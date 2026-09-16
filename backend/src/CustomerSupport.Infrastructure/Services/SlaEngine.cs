using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Sla;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Selects and applies the SLA policy for a ticket and maintains its clocks (SLA and Automation /
/// Response and resolution targets). Replaces the placeholder no-op engine every call site already
/// depended on through <c>ISlaEngine</c> — this class is the only new thing any caller sees.
/// </summary>
public class SlaEngine(
    AppDbContext db,
    IBusinessCalendarCalculator calendar,
    IConditionEvaluator conditions,
    IDateTimeProvider clock) : ISlaEngine
{
    public async Task ApplyPolicyAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
        {
            return;
        }

        var policy = await SelectPolicyAsync(ticket, ct);
        if (policy is null)
        {
            // No matching policy and no default: the ticket gets no clocks and sorts last
            // everywhere (product rule), rather than looking overdue for a promise nobody made.
            ticket.SlaPolicyId = null;
            ticket.FirstResponseDueAt = null;
            ticket.ResolutionDueAt = null;
            await db.SaveChangesAsync(ct);
            return;
        }

        var target = policy.Targets.FirstOrDefault(t => t.PriorityId == ticket.PriorityId);
        if (target is null)
        {
            // A priority with no target on this policy: same "no clocks" outcome, not a crash —
            // an administrator can add coverage later without this ticket silently misreporting.
            ticket.SlaPolicyId = policy.Id;
            ticket.FirstResponseDueAt = null;
            ticket.ResolutionDueAt = null;
            await RemoveExistingClocksAsync(ticket.Id, ct);
            await db.SaveChangesAsync(ct);
            return;
        }

        var now = clock.UtcNow;
        var firstResponseDue = await calendar.AddWorkingMinutesAsync(policy.BusinessCalendarId, now, target.FirstResponseMinutes, ct);
        var resolutionDue = await calendar.AddWorkingMinutesAsync(policy.BusinessCalendarId, now, target.ResolutionMinutes, ct);

        await UpsertClockAsync(ticket.Id, policy, SlaTargetType.FirstResponse, target.FirstResponseMinutes, now, firstResponseDue, ct);
        await UpsertClockAsync(ticket.Id, policy, SlaTargetType.Resolution, target.ResolutionMinutes, now, resolutionDue, ct);

        ticket.SlaPolicyId = policy.Id;
        ticket.FirstResponseDueAt = firstResponseDue;
        ticket.ResolutionDueAt = resolutionDue;
        ticket.IsFirstResponseBreached = false;
        ticket.IsResolutionBreached = false;

        await db.SaveChangesAsync(ct);
    }

    public async Task OnFirstAgentReplyAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticketClock = await db.TicketSlaClocks
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.TargetType == SlaTargetType.FirstResponse, ct);
        if (ticketClock is null || ticketClock.Status != SlaClockStatus.Running)
        {
            return;
        }

        var now = clock.UtcNow;
        var calendarId = await CalendarIdForAsync(ticketClock.SlaPolicyId, ct);

        ticketClock.ElapsedMinutes += await calendar.WorkingMinutesBetweenAsync(calendarId, ticketClock.StartedAt, now, ct);
        ticketClock.MetAt = now;

        // A late first reply is recorded as Breached, not Met — the whole point of tracking it.
        ticketClock.Status = now <= ticketClock.DueAt ? SlaClockStatus.Met : SlaClockStatus.Breached;

        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId, ct);
        ticket.FirstRespondedAt = now;
        if (ticketClock.Status == SlaClockStatus.Breached)
        {
            ticket.IsFirstResponseBreached = true;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task OnStatusChangedAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticketClock = await db.TicketSlaClocks
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.TargetType == SlaTargetType.Resolution, ct);

        // Once breached, the breach stays — restarting the clock would erase it, which is worse
        // than leaving it stopped (product rule).
        if (ticketClock is null || ticketClock.Status == SlaClockStatus.Breached)
        {
            return;
        }

        var ticket = await db.Tickets.Include(t => t.Status).FirstAsync(t => t.Id == ticketId, ct);
        var now = clock.UtcNow;
        var pausesSla = ticket.Status.PausesSla;
        var calendarId = await CalendarIdForAsync(ticketClock.SlaPolicyId, ct);

        if (pausesSla && ticketClock.Status == SlaClockStatus.Running)
        {
            ticketClock.ElapsedMinutes += await calendar.WorkingMinutesBetweenAsync(
                calendarId, ticketClock.PausedAt ?? ticketClock.StartedAt, now, ct);
            ticketClock.PausedAt = now;
            ticketClock.Status = SlaClockStatus.Paused;
        }
        else if (!pausesSla && ticketClock.Status is SlaClockStatus.Paused or SlaClockStatus.Met)
        {
            // Resuming a Paused clock accounts for the pause itself; resuming a Met clock (a
            // ticket reopened after resolution) simply continues from where it was frozen — in
            // both cases the due time is recomputed from what remains, never from the original
            // target, per the "resume never restarts" invariant.
            if (ticketClock.Status == SlaClockStatus.Paused)
            {
                ticketClock.PausedMinutes += await calendar.WorkingMinutesBetweenAsync(
                    calendarId, ticketClock.PausedAt!.Value, now, ct);
                ticketClock.PausedAt = null;
            }
            else
            {
                ticketClock.MetAt = null;
            }

            var remaining = Math.Max(0, ticketClock.TargetMinutes - ticketClock.ElapsedMinutes);
            ticketClock.DueAt = await calendar.AddWorkingMinutesAsync(calendarId, now, remaining, ct);
            ticketClock.Status = SlaClockStatus.Running;
            ticket.ResolutionDueAt = ticketClock.DueAt;
            ticket.IsResolutionBreached = false;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task OnResolvedAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticketClock = await db.TicketSlaClocks
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.TargetType == SlaTargetType.Resolution, ct);
        if (ticketClock is null || ticketClock.Status is SlaClockStatus.Met or SlaClockStatus.Breached)
        {
            return;
        }

        var now = clock.UtcNow;

        if (ticketClock.Status == SlaClockStatus.Running)
        {
            var calendarId = await CalendarIdForAsync(ticketClock.SlaPolicyId, ct);
            ticketClock.ElapsedMinutes += await calendar.WorkingMinutesBetweenAsync(
                calendarId, ticketClock.PausedAt ?? ticketClock.StartedAt, now, ct);
        }
        // A ticket resolved while Paused was, by definition, not late on a clock that was not
        // running — it is recorded as Met regardless of the (stale) due time.

        ticketClock.MetAt = now;
        ticketClock.PausedAt = null;
        ticketClock.Status = now <= ticketClock.DueAt || ticketClock.Status == SlaClockStatus.Paused
            ? SlaClockStatus.Met
            : SlaClockStatus.Breached;

        if (ticketClock.Status == SlaClockStatus.Breached)
        {
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId, ct);
            ticket.IsResolutionBreached = true;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The clock stores which policy applied, not which calendar — resolve it every time
    /// rather than duplicating the calendar id onto the clock row.</summary>
    private async Task<Guid> CalendarIdForAsync(Guid policyId, CancellationToken ct) =>
        await db.SlaPolicies.Where(p => p.Id == policyId).Select(p => p.BusinessCalendarId).FirstAsync(ct);

    /// <summary>
    /// Active policies in <see cref="SlaPolicy.EvaluationOrder"/>, first whose conditions all match;
    /// otherwise the <see cref="SlaPolicy.IsDefault"/> policy.
    /// </summary>
    private async Task<SlaPolicy?> SelectPolicyAsync(Domain.Tickets.Ticket ticket, CancellationToken ct)
    {
        var policies = await db.SlaPolicies
            .Include(p => p.Targets)
            .Include(p => p.Conditions)
            .Where(p => p.IsActive && (p.BranchId == null || p.BranchId == ticket.BranchId))
            .OrderBy(p => p.EvaluationOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var context = new TicketEvaluationContext(ticket, ticket.Category, ticket.Priority, ticket.Customer);

        foreach (var policy in policies.Where(p => !p.IsDefault))
        {
            var specs = policy.Conditions
                .Select(c => new ConditionSpec(c.Field, c.Operator, c.Value))
                .ToList();

            if (specs.Count == 0 || conditions.Matches(specs, context))
            {
                return policy;
            }
        }

        return policies.FirstOrDefault(p => p.IsDefault);
    }

    private async Task UpsertClockAsync(
        Guid ticketId, SlaPolicy policy, SlaTargetType targetType, int targetMinutes,
        DateTimeOffset now, DateTimeOffset dueAt, CancellationToken ct)
    {
        var existing = await db.TicketSlaClocks
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.TargetType == targetType, ct);

        if (existing is null)
        {
            db.TicketSlaClocks.Add(new TicketSlaClock
            {
                TicketId = ticketId,
                SlaPolicyId = policy.Id,
                TargetType = targetType,
                Status = SlaClockStatus.Running,
                StartedAt = now,
                DueAt = dueAt,
                TargetMinutes = targetMinutes,
            });
            return;
        }

        // A re-applied policy (priority change) resets the clock rather than accumulating stale
        // state — the unique (TicketId, TargetType) index means this must update, never insert.
        existing.SlaPolicyId = policy.Id;
        existing.Status = SlaClockStatus.Running;
        existing.StartedAt = now;
        existing.DueAt = dueAt;
        existing.TargetMinutes = targetMinutes;
        existing.ElapsedMinutes = 0;
        existing.PausedMinutes = 0;
        existing.PausedAt = null;
        existing.MetAt = null;
        existing.BreachedAt = null;
        existing.WarningSent = false;
    }

    private async Task RemoveExistingClocksAsync(Guid ticketId, CancellationToken ct)
    {
        var existing = await db.TicketSlaClocks.Where(c => c.TicketId == ticketId).ToListAsync(ct);
        db.TicketSlaClocks.RemoveRange(existing);
    }
}
