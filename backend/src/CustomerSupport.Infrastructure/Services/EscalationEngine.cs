using System.Diagnostics;
using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Evaluates <see cref="EscalationRule"/>s against their trigger's candidates and fires the
/// configured action (SLA and Automation / Escalation rules). Called every 5 minutes by
/// <c>EscalationEvaluationJob</c> — tickets escalate on their own whether or not anyone is watching.
/// </summary>
public class EscalationEngine(
    AppDbContext db,
    IBusinessCalendarCalculator calendar,
    IRuleEvaluator ruleEvaluator,
    ITicketEventRecorder events,
    INotificationDispatcher notifications,
    IDateTimeProvider clock,
    ILogger<EscalationEngine> logger) : IEscalationEngine
{
    /// <summary>Per rule, per run — stops one broad rule from monopolising the 5-minute tick.</summary>
    private const int MaxCandidatesPerRulePerRun = 1000;
    private const int BatchSize = 200;

    public async Task<int> EvaluateAsync(CancellationToken ct = default)
    {
        var rules = await db.EscalationRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.EvaluationOrder).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var rulesFired = 0;

        foreach (var rule in rules)
        {
            var stopwatch = Stopwatch.StartNew();
            var candidates = await LoadCandidatesAsync(rule, ct);
            var fired = 0;

            foreach (var ticket in candidates)
            {
                var (outcome, reason) = await EvaluateOneAsync(rule, ticket, ct);
                if (outcome == "Matched")
                {
                    fired++;
                }
            }

            if (fired > 0)
            {
                rule.FireCount += fired;
                rule.LastFiredAt = clock.UtcNow;
                rulesFired++;
            }

            await db.SaveChangesAsync(ct);
            stopwatch.Stop();

            if (stopwatch.Elapsed > TimeSpan.FromMinutes(5))
            {
                logger.LogWarning(
                    "Escalation rule {RuleName} took {Elapsed} to evaluate {Count} candidate(s) — longer than the job's own interval. The rule set has outgrown the schedule.",
                    rule.Name.En, stopwatch.Elapsed, candidates.Count);
            }

            logger.LogInformation(
                "Escalation rule {RuleName}: {Candidates} candidate(s), {Fired} fired, {Elapsed}ms.",
                rule.Name.En, candidates.Count, fired, stopwatch.ElapsedMilliseconds);
        }

        return rulesFired;
    }

    public async Task<IReadOnlyList<EscalationTestMatch>> TestRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await db.EscalationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (rule is null)
        {
            return [];
        }

        var candidates = await LoadCandidatesAsync(rule, ct);
        var matches = new List<EscalationTestMatch>();

        foreach (var ticket in candidates)
        {
            var context = await BuildContextAsync(ticket, ct);
            if (!ruleEvaluator.Matches(rule.ConditionsJson, context))
            {
                continue;
            }

            matches.Add(new EscalationTestMatch(ticket.Id, ticket.Number, DescribeTrigger(rule, ticket)));
        }

        return matches;
    }

    /// <summary>
    /// Runs the full pipeline for one candidate: extra conditions, cooldown/fire-cap, then the
    /// action. Returns the outcome and reason exactly as recorded in the decision log.
    /// </summary>
    private async Task<(string Outcome, string Reason)> EvaluateOneAsync(EscalationRule rule, Ticket ticket, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        string outcome;
        string reason;
        string? error = null;

        try
        {
            var context = await BuildContextAsync(ticket, ct);
            if (!ruleEvaluator.Matches(rule.ConditionsJson, context))
            {
                (outcome, reason) = ("Skipped", "Conditions did not match");
            }
            else
            {
                var since = clock.UtcNow.AddMinutes(-rule.CooldownMinutes);
                var recentlyFired = await db.AutomationRunLogs.AnyAsync(l =>
                    l.TicketId == ticket.Id && l.RuleId == rule.Id && l.Outcome == "Matched" && l.OccurredAt >= since, ct);

                if (recentlyFired)
                {
                    (outcome, reason) = ("Skipped", $"Cooldown active ({rule.CooldownMinutes} min).");
                }
                else if (rule.MaxFiresPerTicket > 0 &&
                         await db.AutomationRunLogs.CountAsync(l =>
                             l.TicketId == ticket.Id && l.RuleId == rule.Id && l.Outcome == "Matched", ct) >= rule.MaxFiresPerTicket)
                {
                    (outcome, reason) = ("Skipped", $"Max fires reached ({rule.MaxFiresPerTicket}).");
                }
                else
                {
                    (outcome, reason) = await ExecuteActionAsync(rule, ticket, ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Escalation rule {RuleId} failed while evaluating ticket {TicketId}.", rule.Id, ticket.Id);
            outcome = "Failed";
            reason = "See error.";
            error = ex.Message;
        }

        db.AutomationRunLogs.Add(new AutomationRunLog
        {
            RuleType = nameof(EscalationRule),
            RuleId = rule.Id,
            RuleName = rule.Name.En,
            TicketId = ticket.Id,
            Outcome = outcome,
            Reason = reason,
            Error = error,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            OccurredAt = clock.UtcNow,
        });

        return (outcome, reason);
    }

    /// <summary>
    /// Performs the configured action. "Matched" always means the action executed and a ticket
    /// event was appended attributed to the rule; "Skipped" (only possible for RaisePriority at the
    /// top level) is a recorded reason, never an exception.
    /// </summary>
    private async Task<(string Outcome, string Reason)> ExecuteActionAsync(EscalationRule rule, Ticket ticket, CancellationToken ct)
    {
        var ruleName = rule.Name.En;

        switch (rule.Action)
        {
            case EscalationActionType.NotifyManager:
            {
                var recipients = new HashSet<Guid>();
                if (ticket.DepartmentId is { } deptId)
                {
                    var managerId = await db.Departments.Where(d => d.Id == deptId).Select(d => d.ManagerId).FirstOrDefaultAsync(ct);
                    if (managerId is { } mid) recipients.Add(mid);
                }

                if (rule.ActionNotifyRoleId is { } roleId)
                {
                    var roleHolders = await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(ct);
                    foreach (var id in roleHolders) recipients.Add(id);
                }

                foreach (var userId in recipients)
                {
                    await AddWatcherIfMissingAsync(ticket.Id, userId, ct);
                    await notifications.DispatchAsync(
                        userId, "ticket.escalated",
                        $"Ticket escalated: {ticket.Number}", $"تصعيد التذكرة: {ticket.Number}",
                        $"\"{ruleName}\" escalated ticket {ticket.Number}.", $"قامت القاعدة \"{ruleName}\" بتصعيد التذكرة {ticket.Number}.",
                        link: $"/agent/tickets/{ticket.Id}", severity: "Warning", ct: ct);
                }

                events.Record(ticket.Id, TicketEventType.Escalated, newDisplay: "Manager notified", triggeredByRule: ruleName);
                return ("Matched", recipients.Count > 0 ? $"Notified {recipients.Count} manager/role holder(s)." : "No manager or role holder found to notify.");
            }

            case EscalationActionType.Reassign:
            {
                var previousAgentId = ticket.AssignedAgentId;
                if (rule.ActionTargetUserId is { } userId)
                {
                    ticket.AssignedAgentId = userId;
                    ticket.AssignedAt = clock.UtcNow;
                }
                else if (rule.ActionTargetTeamId is { } teamId)
                {
                    // Escalating to a team's queue, not a specific member — round-robin is a routing
                    // decision (CS-502's job); an escalation just needs eyes on it fast.
                    ticket.AssignedTeamId = teamId;
                    ticket.AssignedAgentId = null;
                }
                else
                {
                    return ("Skipped", "Reassign action has no target user or team configured.");
                }

                events.Record(ticket.Id, TicketEventType.Assigned,
                    field: nameof(Ticket.AssignedAgentId),
                    oldValue: previousAgentId?.ToString(), newValue: ticket.AssignedAgentId?.ToString(),
                    triggeredByRule: ruleName);
                return ("Matched", "Reassigned.");
            }

            case EscalationActionType.RaisePriority:
            {
                var current = await db.TicketPriorities.FirstAsync(p => p.Id == ticket.PriorityId, ct);
                var next = await db.TicketPriorities
                    .Where(p => p.IsActive && p.Level > current.Level)
                    .OrderBy(p => p.Level)
                    .FirstOrDefaultAsync(ct);

                if (next is null)
                {
                    return ("Skipped", $"Ticket is already at the highest priority ({current.Name.En}).");
                }

                ticket.PriorityId = next.Id;
                events.Record(ticket.Id, TicketEventType.PriorityChanged,
                    field: nameof(Ticket.PriorityId),
                    oldValue: current.Id.ToString(), newValue: next.Id.ToString(),
                    oldDisplay: current.Name.En, newDisplay: next.Name.En,
                    triggeredByRule: ruleName);
                return ("Matched", $"Raised priority from {current.Name.En} to {next.Name.En}.");
            }

            case EscalationActionType.ChangeDepartment:
            {
                if (rule.ActionTargetDepartmentId is not { } targetDepartmentId)
                {
                    return ("Skipped", "ChangeDepartment action has no target department configured.");
                }

                var target = await db.Departments.FirstOrDefaultAsync(d => d.Id == targetDepartmentId && d.IsActive, ct);
                if (target is null)
                {
                    return ("Skipped", "Target department is missing or inactive.");
                }

                var previousDepartmentId = ticket.DepartmentId;
                var previousName = previousDepartmentId is null
                    ? null
                    : await db.Departments.Where(d => d.Id == previousDepartmentId).Select(d => d.Name.En).FirstOrDefaultAsync(ct);

                // Mirrors the manual transfer exactly: clear the assignee, never touch the SLA clock.
                ticket.DepartmentId = target.Id;
                ticket.AssignedTeamId = null;
                ticket.AssignedAgentId = null;
                ticket.AssignedAt = null;

                events.Record(ticket.Id, TicketEventType.DepartmentChanged,
                    field: nameof(Ticket.DepartmentId),
                    oldValue: previousDepartmentId?.ToString(), newValue: target.Id.ToString(),
                    oldDisplay: previousName, newDisplay: target.Name.En,
                    triggeredByRule: ruleName);
                return ("Matched", $"Transferred to {target.Name.En}.");
            }

            case EscalationActionType.IncreaseEscalationLevel:
            {
                ticket.EscalationLevel += 1;
                ticket.EscalatedAt = clock.UtcNow;
                events.Record(ticket.Id, TicketEventType.Escalated,
                    field: nameof(Ticket.EscalationLevel), newValue: ticket.EscalationLevel.ToString(),
                    triggeredByRule: ruleName);
                return ("Matched", $"Escalation level raised to {ticket.EscalationLevel}.");
            }

            case EscalationActionType.AddWatcher:
            {
                if (rule.ActionTargetUserId is not { } watcherUserId)
                {
                    return ("Skipped", "AddWatcher action has no target user configured.");
                }

                var added = await AddWatcherIfMissingAsync(ticket.Id, watcherUserId, ct);
                events.Record(ticket.Id, TicketEventType.WatcherAdded, newValue: watcherUserId.ToString(), triggeredByRule: ruleName);
                return ("Matched", added ? "Watcher added." : "Already watching.");
            }

            default:
                return ("Skipped", "Unrecognised action.");
        }
    }

    private async Task<bool> AddWatcherIfMissingAsync(Guid ticketId, Guid userId, CancellationToken ct)
    {
        var exists = await db.TicketWatchers.AnyAsync(w => w.TicketId == ticketId && w.UserId == userId, ct);
        if (exists)
        {
            return false;
        }

        db.TicketWatchers.Add(new TicketWatcher
        {
            TicketId = ticketId,
            UserId = userId,
            AddedByAutomation = true,
            AddedAt = clock.UtcNow,
        });
        return true;
    }

    private async Task<List<Ticket>> LoadCandidatesAsync(EscalationRule rule, CancellationToken ct)
    {
        var results = new List<Ticket>();
        var now = clock.UtcNow;

        switch (rule.Trigger)
        {
            case EscalationTrigger.Breached:
            {
                results = await db.Tickets
                    .Where(t => !t.Status.IsTerminal && (t.IsResolutionBreached || t.IsFirstResponseBreached))
                    .OrderBy(t => t.Id)
                    .Take(MaxCandidatesPerRulePerRun)
                    .ToListAsync(ct);
                break;
            }

            case EscalationTrigger.CustomerReplyCount:
            {
                results = await db.Tickets
                    .Where(t => !t.Status.IsTerminal && t.CustomerReplyCount >= (rule.ThresholdCount ?? int.MaxValue))
                    .OrderBy(t => t.Id)
                    .Take(MaxCandidatesPerRulePerRun)
                    .ToListAsync(ct);
                break;
            }

            case EscalationTrigger.ReopenCount:
            {
                results = await db.Tickets
                    .Where(t => t.ReopenCount >= (rule.ThresholdCount ?? int.MaxValue))
                    .OrderBy(t => t.Id)
                    .Take(MaxCandidatesPerRulePerRun)
                    .ToListAsync(ct);
                break;
            }

            case EscalationTrigger.ApproachingBreach:
            {
                // Percentage-consumed is only exact against LIVE working-minute arithmetic, which SQL
                // cannot express — pull the (cheap) set of running clocks, then filter precisely below.
                var runningClocks = await db.TicketSlaClocks
                    .Include(c => c.Ticket)
                    .Where(c => c.Status == SlaClockStatus.Running && c.TargetMinutes > 0
                        && (rule.TargetType == null || c.TargetType == rule.TargetType)
                        && !c.Ticket.Status.IsTerminal)
                    .Take(MaxCandidatesPerRulePerRun)
                    .ToListAsync(ct);

                foreach (var clockRow in runningClocks)
                {
                    var calendarId = await CalendarIdForTicketAsync(clockRow.Ticket, ct);
                    var elapsed = clockRow.ElapsedMinutes
                        + await calendar.WorkingMinutesBetweenAsync(calendarId, clockRow.PausedAt ?? clockRow.StartedAt, now, ct);
                    var consumedPercent = elapsed * 100 / clockRow.TargetMinutes;

                    if (consumedPercent >= (rule.ThresholdPercent ?? 100))
                    {
                        results.Add(clockRow.Ticket);
                    }
                }
                break;
            }

            case EscalationTrigger.NoAgentResponse:
            {
                var thresholdMinutes = rule.ThresholdMinutes ?? int.MaxValue;
                // Wall-clock elapsed is always >= working-minutes elapsed, so filtering on it first is
                // a safe superset — no ticket that could match is excluded here.
                var wallClockCutoff = now.AddMinutes(-thresholdMinutes);

                var prefiltered = await db.Tickets
                    .Where(t => !t.Status.IsTerminal &&
                        ((t.LastAgentReplyAt == null && t.CreatedAt <= wallClockCutoff) ||
                         (t.LastAgentReplyAt != null && t.LastAgentReplyAt <= wallClockCutoff)))
                    .OrderBy(t => t.Id)
                    .Take(MaxCandidatesPerRulePerRun)
                    .ToListAsync(ct);

                foreach (var ticket in prefiltered)
                {
                    var calendarId = await CalendarIdForTicketAsync(ticket, ct);
                    var referencePoint = ticket.LastAgentReplyAt ?? ticket.CreatedAt;
                    var idleWorkingMinutes = await calendar.WorkingMinutesBetweenAsync(calendarId, referencePoint, now, ct);

                    if (idleWorkingMinutes >= thresholdMinutes)
                    {
                        results.Add(ticket);
                    }
                }
                break;
            }
        }

        return results;
    }

    private async Task<Guid> CalendarIdForTicketAsync(Ticket ticket, CancellationToken ct)
    {
        if (ticket.SlaPolicyId is { } policyId)
        {
            var calendarId = await db.SlaPolicies.Where(p => p.Id == policyId).Select(p => (Guid?)p.BusinessCalendarId).FirstOrDefaultAsync(ct);
            if (calendarId is { } id)
            {
                return id;
            }
        }

        return await db.BusinessCalendars.Where(c => c.IsDefault).Select(c => c.Id).FirstAsync(ct);
    }

    private async Task<TicketEvaluationContext> BuildContextAsync(Ticket ticket, CancellationToken ct)
    {
        var category = await db.TicketCategories.AsNoTracking().FirstAsync(c => c.Id == ticket.CategoryId, ct);
        var priority = await db.TicketPriorities.AsNoTracking().FirstAsync(p => p.Id == ticket.PriorityId, ct);
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == ticket.CustomerId, ct);
        return new TicketEvaluationContext(ticket, category, priority, customer);
    }

    private static string DescribeTrigger(EscalationRule rule, Ticket ticket) => rule.Trigger switch
    {
        EscalationTrigger.ApproachingBreach => $"Approaching its SLA target beyond {rule.ThresholdPercent}%.",
        EscalationTrigger.Breached => "SLA already breached.",
        EscalationTrigger.NoAgentResponse => $"No agent reply for at least {rule.ThresholdMinutes} working minute(s).",
        EscalationTrigger.CustomerReplyCount => $"Customer has replied {ticket.CustomerReplyCount} time(s) without resolution.",
        EscalationTrigger.ReopenCount => $"Ticket has been reopened {ticket.ReopenCount} time(s).",
        _ => "Matches the trigger.",
    };
}
