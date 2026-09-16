using System.Diagnostics;
using System.Text.Json;
using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Evaluates <see cref="AssignmentRule"/>s in order and assigns the ticket to the agent the first
/// matching rule's strategy picks (SLA and Automation / Automatic assignment). Every rule evaluated —
/// matched, skipped or failed — is logged, which is what makes the rule tester and the "why was this
/// assigned here" question answerable later.
/// </summary>
public class AssignmentEngine(
    AppDbContext db,
    IRuleEvaluator ruleEvaluator,
    IAgentCapacityService capacityService,
    IAgentDirectory agentDirectory,
    ITicketEventRecorder events,
    INotificationDispatcher notifications,
    IDateTimeProvider clock,
    ILogger<AssignmentEngine> logger) : IAssignmentEngine
{
    public async Task<Guid?> AssignAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
        {
            return null;
        }

        var rules = await db.AssignmentRules
            .Where(r => r.IsActive && (r.BranchId == null || r.BranchId == ticket.BranchId))
            .OrderBy(r => r.EvaluationOrder).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var context = new TicketEvaluationContext(ticket, ticket.Category, ticket.Priority, ticket.Customer);
        var logs = new List<AutomationRunLog>();
        Guid? assignedAgentId = null;

        foreach (var rule in rules)
        {
            var stopwatch = Stopwatch.StartNew();
            AutomationRunLog log;

            try
            {
                var matched = ruleEvaluator.Matches(rule.ConditionsJson, context);
                if (!matched)
                {
                    log = NewLog(rule, ticket.Id, "Skipped", "Conditions did not match", null, stopwatch);
                    logs.Add(log);
                    continue;
                }

                var (chosenAgentId, candidateCount, eligibleCount) = await ResolveCandidateAsync(rule, ticket, ct);
                string reason;

                if (chosenAgentId is { } agentId)
                {
                    var chosen = await agentDirectory.GetAsync(agentId, ct);
                    ApplyAssignment(ticket, rule.TargetTeamId, agentId);
                    reason = $"Assigned to {chosen?.DisplayName.En ?? agentId.ToString()} via {rule.Strategy}";
                    assignedAgentId = agentId;
                }
                else
                {
                    reason = candidateCount == 0
                        ? "No candidates found for this rule's target."
                        : $"No eligible candidate among {candidateCount} ({eligibleCount} passed capacity/availability) — left in queue.";
                }

                rule.MatchCount += 1;
                rule.LastMatchedAt = clock.UtcNow;

                log = NewLog(rule, ticket.Id, "Matched", reason,
                    JsonSerializer.Serialize(new { chosenAgentId, candidateCount, eligibleCount }), stopwatch);
                logs.Add(log);

                if (assignedAgentId is not null)
                {
                    break; // a chosen agent conclusively ends routing, regardless of StopProcessing
                }

                if (rule.StopProcessing)
                {
                    break; // matched but produced no agent, and this rule does not allow fallthrough
                }
                // StopProcessing == false and nobody was eligible: let the next rule try (layered fallback).
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Assignment rule {RuleId} failed while evaluating ticket {TicketId}.", rule.Id, ticket.Id);
                log = NewLog(rule, ticket.Id, "Failed", null, null, stopwatch);
                log.Error = ex.Message;
                logs.Add(log);
            }
        }

        if (assignedAgentId is { } finalAgentId)
        {
            events.Record(ticket.Id, TicketEventType.Assigned,
                field: nameof(Ticket.AssignedAgentId),
                newValue: finalAgentId.ToString(),
                newDisplay: (await agentDirectory.GetAsync(finalAgentId, ct))?.DisplayName.En,
                triggeredByRule: "automatic-assignment");
        }

        db.AutomationRunLogs.AddRange(logs);
        await db.SaveChangesAsync(ct);

        if (assignedAgentId is { } notifyAgentId)
        {
            await TicketAssignmentNotifications.Assigned(notifications, ticket, notifyAgentId, ct);
            await db.SaveChangesAsync(ct);
        }

        return assignedAgentId;
    }

    public async Task<AssignmentPreview> PreviewRuleAsync(Guid ticketId, Guid ruleId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        var rule = await db.AssignmentRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ruleId, ct);

        if (ticket is null || rule is null)
        {
            return new AssignmentPreview(false, null, 0, 0, "Ticket or rule not found.");
        }

        var context = new TicketEvaluationContext(ticket, ticket.Category, ticket.Priority, ticket.Customer);
        var matched = ruleEvaluator.Matches(rule.ConditionsJson, context);
        if (!matched)
        {
            return new AssignmentPreview(false, null, 0, 0, "Conditions did not match this ticket.");
        }

        var (chosenAgentId, candidateCount, eligibleCount) = await ResolveCandidateAsync(rule, ticket, ct);
        var reason = chosenAgentId is { } agentId
            ? $"Would assign to {(await agentDirectory.GetAsync(agentId, ct))?.DisplayName.En ?? agentId.ToString()} via {rule.Strategy}"
            : candidateCount == 0
                ? "No candidates found for this rule's target."
                : $"No eligible candidate among {candidateCount} ({eligibleCount} passed capacity/availability) — would stay queued.";

        return new AssignmentPreview(true, chosenAgentId, candidateCount, eligibleCount, reason);
    }

    private static void ApplyAssignment(Ticket ticket, Guid? teamId, Guid agentId)
    {
        ticket.AssignedAgentId = agentId;
        ticket.AssignedTeamId = teamId ?? ticket.AssignedTeamId;
        ticket.AssignedAt = DateTimeOffset.UtcNow;
    }

    private static AutomationRunLog NewLog(
        AssignmentRule rule, Guid ticketId, string outcome, string? reason, string? resultJson, Stopwatch stopwatch) => new()
    {
        RuleType = nameof(AssignmentRule),
        RuleId = rule.Id,
        RuleName = rule.Name.En,
        TicketId = ticketId,
        Outcome = outcome,
        Reason = reason,
        ResultJson = resultJson,
        DurationMs = (int)stopwatch.ElapsedMilliseconds,
        OccurredAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Builds the candidate list for one rule's target, filters by capacity/availability, and applies
    /// the strategy. Returns the chosen agent (or null to leave the ticket queued) plus the raw and
    /// eligible candidate counts the decision log and rule tester report.
    /// </summary>
    private async Task<(Guid? ChosenAgentId, int CandidateCount, int EligibleCount)> ResolveCandidateAsync(
        AssignmentRule rule, Ticket ticket, CancellationToken ct)
    {
        if (rule.Strategy == AssignmentStrategy.QueueOnly)
        {
            return (null, 0, 0);
        }

        if (rule.Strategy == AssignmentStrategy.Direct)
        {
            if (rule.TargetUserId is not { } directUserId)
            {
                return (null, 0, 0);
            }

            if (!rule.RespectAgentAvailability)
            {
                return (directUserId, 1, 1);
            }

            var capacity = await capacityService.CheckAsync(directUserId, rule.TargetTeamId, ct);
            return capacity.IsAvailable ? (directUserId, 1, 1) : (directUserId, 1, 0);
        }

        List<Guid> candidateUserIds;
        if (rule.TargetTeamId is { } teamId)
        {
            candidateUserIds = await db.TeamMembers
                .Where(m => m.TeamId == teamId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync(ct);
        }
        else if (rule.TargetDepartmentId is { } departmentId)
        {
            candidateUserIds = (await agentDirectory.GetAgentIdsByDepartmentAsync(departmentId, ct)).ToList();
        }
        else
        {
            return (null, 0, 0);
        }

        if (candidateUserIds.Count == 0)
        {
            return (null, 0, 0);
        }

        var eligible = new List<Guid>();
        foreach (var userId in candidateUserIds)
        {
            if (!rule.RespectAgentAvailability)
            {
                eligible.Add(userId);
                continue;
            }

            var capacity = await capacityService.CheckAsync(userId, rule.TargetTeamId, ct);
            if (capacity.IsAvailable)
            {
                eligible.Add(userId);
            }
        }

        if (eligible.Count == 0)
        {
            return (null, candidateUserIds.Count, 0);
        }

        var chosen = rule.Strategy switch
        {
            AssignmentStrategy.RoundRobin when rule.TargetTeamId is { } rrTeamId => await PickRoundRobinAsync(rrTeamId, eligible, ct),
            AssignmentStrategy.RoundRobin => null, // round robin needs a team's rotation cursor; a department-only rule cannot use it
            AssignmentStrategy.LoadBalanced => await PickLoadBalancedAsync(eligible, rule.TargetTeamId, ct),
            AssignmentStrategy.SkillBased => await PickSkillBasedAsync(eligible, ticket.CategoryId, ct),
            _ => null,
        };

        return (chosen, candidateUserIds.Count, eligible.Count);
    }

    /// <summary>
    /// Advances the team's rotation cursor via an atomic compare-and-swap (<c>ExecuteUpdateAsync</c>
    /// with the expected current value in the WHERE clause) rather than a pessimistic row lock — the
    /// dev database runs on Sqlite, which has no <c>WITH (UPDLOCK, ROWLOCK)</c> equivalent, and a
    /// single-statement conditional UPDATE gives the same "two concurrent tickets never read the same
    /// cursor" guarantee on both providers.
    /// </summary>
    private async Task<Guid?> PickRoundRobinAsync(Guid teamId, List<Guid> eligibleUserIds, CancellationToken ct)
    {
        var ordered = await db.TeamMembers
            .Where(m => m.TeamId == teamId && eligibleUserIds.Contains(m.UserId))
            .OrderBy(m => m.RotationOrder).ThenBy(m => m.UserId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (ordered.Count == 0)
        {
            return null;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var cursor = await db.Teams.Where(t => t.Id == teamId).Select(t => t.RoundRobinCursor).FirstAsync(ct);
            var chosen = ordered[cursor % ordered.Count];
            var nextCursor = (cursor + 1) % ordered.Count;

            var updated = await db.Teams
                .Where(t => t.Id == teamId && t.RoundRobinCursor == cursor)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RoundRobinCursor, nextCursor), ct);

            if (updated > 0)
            {
                return chosen;
            }
            // Another concurrent assignment advanced the cursor between the read and the write — retry.
        }

        // Sustained contention across 5 attempts is not realistic outside a stress test; fall back to
        // a best-effort (non-atomic) pick rather than leaving a real ticket unassigned over it.
        var fallbackCursor = await db.Teams.Where(t => t.Id == teamId).Select(t => t.RoundRobinCursor).FirstAsync(ct);
        return ordered[fallbackCursor % ordered.Count];
    }

    private async Task<Guid?> PickLoadBalancedAsync(List<Guid> eligibleUserIds, Guid? teamId, CancellationToken ct)
    {
        var openCounts = await OpenTicketCountsAsync(eligibleUserIds, ct);

        var rotationOrders = teamId is { } tid
            ? await db.TeamMembers.Where(m => m.TeamId == tid && eligibleUserIds.Contains(m.UserId))
                .ToDictionaryAsync(m => m.UserId, m => m.RotationOrder, ct)
            : new Dictionary<Guid, int>();

        return eligibleUserIds
            .OrderBy(id => openCounts.GetValueOrDefault(id, 0))
            .ThenBy(id => rotationOrders.GetValueOrDefault(id, int.MaxValue))
            .ThenBy(id => id)
            .FirstOrDefault();
    }

    private async Task<Guid?> PickSkillBasedAsync(List<Guid> eligibleUserIds, Guid categoryId, CancellationToken ct)
    {
        var skilled = await db.AgentSkills
            .Where(s => s.CategoryId == categoryId && eligibleUserIds.Contains(s.UserId))
            .ToListAsync(ct);

        if (skilled.Count == 0)
        {
            return null;
        }

        var openCounts = await OpenTicketCountsAsync(skilled.Select(s => s.UserId), ct);

        return skilled
            .OrderByDescending(s => s.Level)
            .ThenBy(s => openCounts.GetValueOrDefault(s.UserId, 0))
            .Select(s => s.UserId)
            .FirstOrDefault();
    }

    private async Task<Dictionary<Guid, int>> OpenTicketCountsAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        return await db.Tickets
            .Where(t => t.AssignedAgentId != null && ids.Contains(t.AssignedAgentId.Value) && !t.Status.IsTerminal)
            .GroupBy(t => t.AssignedAgentId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);
    }
}
