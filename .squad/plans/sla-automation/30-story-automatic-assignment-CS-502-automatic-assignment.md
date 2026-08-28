# Story 30 — Assignment rules and routing strategies (Story: CS-502-automatic-assignment)

## Prerequisites

- CS-203 must be complete — **reuse `AgentCapacityService`**, do not reimplement capacity.
- CS-1203 must be complete for teams, rotation order and per-member caps.
- This story builds `IRuleEvaluator`, which CS-503 reuses. Design it for both.

## Story Goal

Tickets route themselves. Rules evaluate in order, strategies pick an agent, availability and capacity
are respected, and every decision is logged so a manager can answer "why did this go to Ahmed".

## Context — Read These Files First

1. `.squad/stories/sla-automation/CS-502-automatic-assignment/intake.md`.
2. [backend/src/CustomerSupport.Domain/Automation/AssignmentRule.cs](backend/src/CustomerSupport.Domain/Automation/AssignmentRule.cs) — `ConditionsJson`, `Strategy`, `RespectAgentAvailability`, `StopProcessing`.
3. [backend/src/CustomerSupport.Domain/Enums/SlaEnums.cs](backend/src/CustomerSupport.Domain/Enums/SlaEnums.cs) — `AssignmentStrategy` with per-member documentation.
4. [backend/src/CustomerSupport.Domain/Organization/Team.cs](backend/src/CustomerSupport.Domain/Organization/Team.cs) — `RoundRobinCursor` and its remark about the row lock.
5. [backend/src/CustomerSupport.Domain/Automation/AutomationRunLog.cs](backend/src/CustomerSupport.Domain/Automation/AutomationRunLog.cs) — the decision log.
6. `AgentCapacityService` from CS-203 — the single capacity rule.

## Product rules (from story)

- **First match wins**, unless the rule sets `StopProcessing = false`.
- **Round robin advances the cursor under a row lock.** Two tickets arriving in the same second must not get the same agent.
- **When every candidate is ineligible, leave the ticket in the queue.** Force-assigning to an away or overloaded agent is worse than leaving it unassigned, and hides the staffing problem.
- **Every evaluation writes an `AutomationRunLog` row** — matched, skipped or failed — with a human-readable reason.
- **Capacity and availability come from `AgentCapacityService`.** One rule, two callers.
- **Assignment failure never fails ticket creation.** Log it and leave the ticket unassigned.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Shared rule evaluator

**File:** `backend/src/CustomerSupport.Application/Automation/IRuleEvaluator.cs`

Shared with CS-503. Conditions are stored as JSON with the same shape as `SlaPolicyCondition`:

```json
[{ "field": "Priority.Code", "operator": "In", "value": "high,urgent" },
 { "field": "Customer.Tier", "operator": "Equals", "value": "Gold" }]
```

```csharp
public interface IRuleEvaluator
{
    /// <summary>All conditions must hold (AND). An unknown field evaluates false and is logged.</summary>
    bool Matches(string conditionsJson, TicketEvaluationContext context);
}
```

Resolve fields from a fixed, allow-listed dictionary rather than reflection — reflection over arbitrary field paths supplied by an administrator is both slow and a data-exposure risk:

```csharp
private static readonly Dictionary<string, Func<TicketEvaluationContext, string?>> Fields = new()
{
    ["CategoryId"]     = c => c.Ticket.CategoryId.ToString(),
    ["Category.Code"]  = c => c.Category.Code,
    ["Priority.Code"]  = c => c.Priority.Code,
    ["Priority.Level"] = c => c.Priority.Level.ToString(),
    ["Channel"]        = c => ((int)c.Ticket.Channel).ToString(),
    ["DepartmentId"]   = c => c.Ticket.DepartmentId?.ToString(),
    ["Customer.Tier"]  = c => c.Customer.Tier,
    ["Customer.Type"]  = c => ((int)c.Customer.Type).ToString(),
    ["BranchId"]       = c => c.Ticket.BranchId?.ToString(),
    ["Subject"]        = c => c.Ticket.Subject,
};
```

An unknown field returns no match and logs a warning naming the rule, so a typo in a rule is visible rather than silently disabling it.

### 2 — Assignment strategies

**File:** `backend/src/CustomerSupport.Infrastructure/Services/AssignmentEngine.cs`

Build the candidate list from the target team (or department), filter with `AgentCapacityService` when `RespectAgentAvailability`, then apply the strategy.

Round robin, with the cursor advanced under a row lock — this is the part that breaks under concurrency if done naively:

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);

// Lock the team row so two concurrent assignments cannot read the same cursor.
var team = await db.Teams
    .FromSqlInterpolated($"SELECT * FROM Teams WITH (UPDLOCK, ROWLOCK) WHERE Id = {teamId}")
    .FirstAsync(ct);

var ordered = candidates.OrderBy(c => c.RotationOrder).ThenBy(c => c.UserId).ToList();
var chosen = ordered[team.RoundRobinCursor % ordered.Count];

team.RoundRobinCursor = (team.RoundRobinCursor + 1) % ordered.Count;
await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

Load balanced: order candidates by open-ticket count ascending, then by rotation order to break ties deterministically.

Skill based: join `AgentSkill` on the ticket category, keep only agents with a skill row, order by level descending then by open-ticket count.

Direct: the configured user, still subject to the capacity check unless the rule overrides it.

Queue only: return null; the ticket stays in the team queue by design.

### 3 — Decision logging and the rule tester

Write an `AutomationRunLog` row for **every** rule evaluated, not only the one that matched:

```csharp
log.Add(new AutomationRunLog
{
    RuleType = nameof(AssignmentRule), RuleId = rule.Id, RuleName = rule.Name.En,
    TicketId = ticket.Id,
    Outcome = matched ? "Matched" : "Skipped",
    Reason = matched
        ? $"Assigned to {chosenName} via {rule.Strategy}"
        : failedCondition ?? "Conditions did not match",
    ResultJson = JsonSerializer.Serialize(new { chosenAgentId, candidateCount, eligibleCount }),
    DurationMs = (int)sw.ElapsedMilliseconds,
    OccurredAt = clock.UtcNow,
});
```

Logging skipped rules is what makes the tester and the "why" question answerable.

`POST /api/assignment-rules/{id}/test` takes a ticket id or a hypothetical ticket and returns whether it matches, which agent would be chosen, the candidate and eligible counts, and the reason — **without writing anything**.

### 4 — Wire into ticket creation

Call `IAssignmentEngine.AssignAsync` after the ticket and its SLA clocks are committed, never inside the creation transaction:

```csharp
try
{
    await assignment.AssignAsync(ticket.Id, ct);
}
catch (Exception ex)
{
    // A routing failure must never prevent a customer's ticket from existing.
    logger.LogError(ex, "Automatic assignment failed for ticket {TicketId}", ticket.Id);
}
```

## Frontend Tasks

### 5 — Rule builder

**File:** `frontend/src/app/features/admin/automation/assignment-rules/`

An ordered list of rules with drag-to-reorder (rewriting `EvaluationOrder`), an active toggle, and match-count and last-matched columns so a manager can see which rules actually fire.

The editor has a conditions builder — rows of field, operator, value, with the field list fetched from the server so it always matches the allow-list — plus strategy selection with the target field switching by strategy, and the availability and stop-processing toggles.

Explain each strategy inline in one sentence. "Round robin" means nothing to a support manager without it.

### 6 — Rule tester and decision log

A test panel in the editor: pick an existing ticket or fill a hypothetical one, run, and see the outcome with the reason and the candidate breakdown.

A decision log viewer at `/admin/automation/log`, filterable by ticket, rule and outcome. Also surface the decision for a single ticket on its history tab, so "why was this assigned here" is answerable from the ticket itself rather than from an admin screen.

## Verification Steps

1. Create a rule matching high-priority billing tickets, assigning round robin to a team of three: successive tickets cycle through all three in rotation order.
2. Restart the API and create another: the rotation continues from where it left off.
3. Create 10 tickets concurrently: each agent receives roughly a third, and no two tickets share an agent from the same cursor value.
4. Set one agent to Away: they are skipped and the rotation continues.
5. Set every candidate at capacity: the ticket stays unassigned and the log records why.
6. Create a load-balanced rule: the agent with the fewest open tickets is chosen.
7. Create a skill-based rule: only agents with a skill in that category are eligible, and the highest level wins.
8. Create a rule with an unknown field name: it does not match, and a warning naming the rule is logged.
9. Confirm every evaluated rule writes a log row, including skipped ones.
10. Use the tester on a real ticket: it reports the outcome and changes nothing.
11. Force the assignment engine to throw: the ticket is still created, unassigned, and the error is logged.
12. Confirm manual and automatic assignment agree on capacity for the same agent.

## Done Criteria

- [ ] `IRuleEvaluator` uses an allow-listed field map and is reusable by CS-503.
- [ ] All five strategies work, with round robin correct under concurrency via a row lock.
- [ ] Ineligible candidates leave the ticket queued rather than being force-assigned.
- [ ] Every evaluation is logged with a readable reason; the tester writes nothing.
- [ ] Capacity comes from `AgentCapacityService`, shared with manual assignment.
- [ ] Assignment failure never fails ticket creation.
- [ ] The rule builder explains strategies in plain language and shows match counts.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
