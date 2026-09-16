using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Sla;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Detects SLA breaches and approaching-breach warnings without anyone needing a screen open
/// (SLA and Automation / Response and resolution targets). Runs every minute over the
/// <c>(Status, DueAt)</c> index. Two passes, each batched and looped until a pass returns fewer
/// than <see cref="BatchSize"/> rows, so a backlog drains within one tick rather than trickling out
/// one batch per minute.
/// </summary>
[DisallowConcurrentExecution]
public class SlaBreachSweepJob(
    AppDbContext db,
    IDateTimeProvider clock,
    IBusinessCalendarCalculator calendar,
    ITicketEventRecorder events,
    INotificationDispatcher notifications,
    ILogger<SlaBreachSweepJob> logger) : IJob
{
    private const int BatchSize = 500;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var breached = await SweepBreachesAsync(ct);
        var warned = await SweepWarningsAsync(ct);

        if (breached > 0 || warned > 0)
        {
            logger.LogInformation("SLA sweep: {Breached} clock(s) breached, {Warned} warning(s) sent.", breached, warned);
        }
    }

    private async Task<int> SweepBreachesAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var total = 0;

        while (true)
        {
            var batch = await db.TicketSlaClocks
                .Include(c => c.Ticket)
                .Where(c => c.Status == SlaClockStatus.Running && c.DueAt <= now)
                .OrderBy(c => c.DueAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var ticketClock in batch)
            {
                ticketClock.BreachedAt = now;
                ticketClock.Status = SlaClockStatus.Breached;

                var ticket = ticketClock.Ticket;
                if (ticketClock.TargetType == SlaTargetType.FirstResponse)
                {
                    ticket.IsFirstResponseBreached = true;
                }
                else
                {
                    ticket.IsResolutionBreached = true;
                }

                events.Record(ticket.Id, TicketEventType.SlaBreached,
                    field: nameof(TicketSlaClock.TargetType),
                    newValue: ticketClock.TargetType.ToString(),
                    newDisplay: ticketClock.TargetType == SlaTargetType.FirstResponse
                        ? "First response"
                        : "Resolution",
                    triggeredByRule: "sla-breach-sweep");

                if (ticket.AssignedAgentId is { } agentId)
                {
                    await notifications.DispatchAsync(
                        agentId,
                        "sla.breached",
                        $"SLA breached: {ticket.Number}",
                        $"تم خرق اتفاقية مستوى الخدمة: {ticket.Number}",
                        $"The {(ticketClock.TargetType == SlaTargetType.FirstResponse ? "first response" : "resolution")} commitment on ticket {ticket.Number} has been breached.",
                        $"تم تجاوز موعد {(ticketClock.TargetType == SlaTargetType.FirstResponse ? "الرد الأول" : "الحل")} للتذكرة {ticket.Number}.",
                        link: $"/agent/tickets/{ticket.Id}",
                        severity: "Critical",
                        ct: ct);
                }
            }

            await db.SaveChangesAsync(ct);
            total += batch.Count;

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return total;
    }

    private async Task<int> SweepWarningsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var total = 0;

        while (true)
        {
            var candidates = await db.TicketSlaClocks
                .Include(c => c.Ticket)
                .Where(c => c.Status == SlaClockStatus.Running && !c.WarningSent)
                .OrderBy(c => c.DueAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                break;
            }

            var policyIds = candidates.Select(c => c.SlaPolicyId).Distinct().ToList();
            var policies = await db.SlaPolicies
                .Where(p => policyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.BusinessCalendarId, p.WarningThresholdPercent })
                .ToDictionaryAsync(p => p.Id, ct);

            var toWarn = new List<TicketSlaClock>();
            foreach (var ticketClock in candidates)
            {
                if (!policies.TryGetValue(ticketClock.SlaPolicyId, out var policy))
                {
                    continue; // the policy was deleted after this clock was created
                }

                var elapsed = ticketClock.ElapsedMinutes
                    + await calendar.WorkingMinutesBetweenAsync(
                        policy.BusinessCalendarId, ticketClock.PausedAt ?? ticketClock.StartedAt, now, ct);
                var consumedPercent = ticketClock.TargetMinutes == 0 ? 0 : elapsed * 100 / ticketClock.TargetMinutes;

                if (consumedPercent >= policy.WarningThresholdPercent)
                {
                    toWarn.Add(ticketClock);
                }
            }

            if (toWarn.Count == 0)
            {
                // Nothing in this page crossed its threshold, and none of these rows changed state
                // (only warned rows leave the "Running && !WarningSent" set), so re-querying would
                // return the identical page forever. Stop here; the next run one minute later will
                // pick up any candidate beyond this page whose consumption has since crossed in.
                break;
            }

            foreach (var ticketClock in toWarn)
            {
                // Set BEFORE dispatching so a retry (or an overlapping sweep on another instance)
                // can never send the warning twice.
                ticketClock.WarningSent = true;

                var ticket = ticketClock.Ticket;
                if (ticket.AssignedAgentId is { } agentId)
                {
                    await notifications.DispatchAsync(
                        agentId,
                        "sla.warning",
                        $"Approaching SLA breach: {ticket.Number}",
                        $"اقتراب خرق اتفاقية مستوى الخدمة: {ticket.Number}",
                        $"Ticket {ticket.Number} is approaching its {(ticketClock.TargetType == SlaTargetType.FirstResponse ? "first response" : "resolution")} deadline.",
                        $"التذكرة {ticket.Number} تقترب من موعدها النهائي.",
                        link: $"/agent/tickets/{ticket.Id}",
                        severity: "Warning",
                        ct: ct);
                }
            }

            await db.SaveChangesAsync(ct);
            total += toWarn.Count;

            if (candidates.Count < BatchSize)
            {
                break;
            }
        }

        return total;
    }
}
