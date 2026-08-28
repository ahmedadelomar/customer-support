# Story 31 — Time-driven escalation rules (Story: CS-503-escalation-rules)

## Prerequisites

- CS-501 must be complete — triggers read SLA clocks.
- CS-502 must be complete — reuse `IRuleEvaluator` and the decision-log pattern.
- CS-504 provides notification delivery for the notify actions.

## Story Goal

Tickets at risk escalate on their own, on a timer, whether or not anyone is watching. Cooldowns and
fire caps stop a rule becoming noise, and every firing is attributed to the rule by name.

## Context — Read These Files First

1. `.squad/stories/sla-automation/CS-503-escalation-rules/intake.md`.
2. [backend/src/CustomerSupport.Domain/Automation/EscalationRule.cs](backend/src/CustomerSupport.Domain/Automation/EscalationRule.cs) — triggers, thresholds, actions, `CooldownMinutes`, `MaxFiresPerTicket`.
3. [backend/src/CustomerSupport.Domain/Enums/SlaEnums.cs](backend/src/CustomerSupport.Domain/Enums/SlaEnums.cs) — `EscalationTrigger` and `EscalationActionType`.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/SlaAndAutomationConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/SlaAndAutomationConfigurations.cs) — `(TicketId, RuleId, OccurredAt)` is the cooldown-check index.
5. `IRuleEvaluator` from CS-502.

## Product rules (from story)

- **Evaluation is job-driven, every 5 minutes.** A rule that only fires when a screen is open is not an escalation rule.
- **Cooldown and fire caps are checked from `AutomationRunLog`** before acting.
- **Raise priority at the top level is a skip with a recorded reason**, not an error.
- **Notify actions add the recipient as a watcher**, so they see what happens next.
- **Every firing appends a ticket event attributed to the rule by name**, never to a person.
- **The tester never fires anything.** It reports which currently open tickets would match.
- **Deactivating a rule stops it immediately**; its history stays.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Trigger evaluation

**File:** `backend/src/CustomerSupport.Infrastructure/Services/EscalationEngine.cs`

Per trigger type, the candidate query:

```csharp
IQueryable<Ticket> Candidates(EscalationRule rule) => rule.Trigger switch
{
    // Elapsed working time has crossed the configured percentage of the target.
    EscalationTrigger.ApproachingBreach => db.Tickets
        .Where(t => !t.Status.IsTerminal)
        .Join(db.TicketSlaClocks.Where(c => c.Status == SlaClockStatus.Running),
              t => t.Id, c => c.TicketId, (t, c) => new { t, c })
        .Where(x => rule.TargetType == null || x.c.TargetType == rule.TargetType)
        .Where(x => x.c.TargetMinutes > 0 &&
                    x.c.ElapsedMinutes * 100 / x.c.TargetMinutes >= rule.ThresholdPercent)
        .Select(x => x.t),

    EscalationTrigger.Breached => db.Tickets
        .Where(t => !t.Status.IsTerminal && (t.IsResolutionBreached || t.IsFirstResponseBreached)),

    // Idle time is measured in WORKING minutes, or every ticket escalates over a weekend.
    EscalationTrigger.NoAgentResponse => db.Tickets
        .Where(t => !t.Status.IsTerminal && t.LastAgentReplyAt == null
                    || t.LastAgentReplyAt < cutoffComputedFromWorkingMinutes),

    EscalationTrigger.CustomerReplyCount => db.Tickets
        .Where(t => !t.Status.IsTerminal && t.CustomerReplyCount >= rule.ThresholdCount),

    EscalationTrigger.ReopenCount => db.Tickets
        .Where(t => t.ReopenCount >= rule.ThresholdCount),

    _ => db.Tickets.Where(_ => false),
};
```

The `NoAgentResponse` cutoff must be computed with `IBusinessCalendarCalculator`, not by subtracting wall-clock minutes — otherwise every ticket escalates on Monday morning.

Then apply `IRuleEvaluator` for the rule's extra conditions.

### 2 — Cooldown, fire cap and actions

Before acting on a ticket:

```csharp
var since = clock.UtcNow.AddMinutes(-rule.CooldownMinutes);

var recent = await db.AutomationRunLogs.AnyAsync(l =>
    l.TicketId == ticket.Id && l.RuleId == rule.Id &&
    l.Outcome == "Matched" && l.OccurredAt >= since, ct);
if (recent) continue;

if (rule.MaxFiresPerTicket > 0)
{
    var fires = await db.AutomationRunLogs.CountAsync(l =>
        l.TicketId == ticket.Id && l.RuleId == rule.Id && l.Outcome == "Matched", ct);
    if (fires >= rule.MaxFiresPerTicket) continue;
}
```

Actions:

- **NotifyManager** — resolve the department manager plus `ActionNotifyRoleId` holders, notify and add as watchers.
- **Reassign** — to `ActionTargetUserId` or via `IAssignmentEngine` against `ActionTargetTeamId`.
- **RaisePriority** — next level up; already at the top means `Outcome = "Skipped"` with the reason recorded.
- **ChangeDepartment** — reuse `TransferTicketCommand` so the SLA clock is preserved exactly as in a manual transfer.
- **IncreaseEscalationLevel** — increment and stamp `EscalatedAt`.
- **AddWatcher** — add with `AddedByAutomation = true`.

Every action appends a ticket event with `TriggeredByRule = rule.Name.En`.

### 3 — Evaluation job

**File:** `backend/src/CustomerSupport.Infrastructure/Jobs/EscalationEvaluationJob.cs`

Quartz, every 5 minutes, `DisallowConcurrentExecution`. Iterate active rules in `EvaluationOrder`, take candidates in batches of 200, and stop a rule's pass after a configured maximum so one broad rule cannot monopolise the run.

Log the pass summary — rules evaluated, tickets matched, duration — so a slow rule is diagnosable. If the run exceeds its interval, log a warning: it means the rule set has outgrown the schedule.

## Frontend Tasks

### 4 — Escalation rule builder

Mirror the assignment rule builder. The trigger selector switches which threshold input is shown — percent, minutes or count — and hides the others, so an administrator cannot set a percentage on a count-based trigger.

The action selector switches its target field similarly. Cooldown and max-fires inputs carry inline help explaining that they exist to stop a rule becoming noise.

Show fire count and last-fired columns in the list.

### 5 — Tester and ticket-level visibility

The tester runs the candidate query in dry-run mode and lists the currently open tickets that would fire, with the reason per ticket — this is how a manager gains confidence before enabling a broad rule.

On the ticket history tab, escalation events show the rule name and the action taken, so the ticket explains itself.

## Verification Steps

1. Create an approaching-breach rule at 80 percent with a notify action. Age a ticket past 80 percent of its target: the rule fires within 5 minutes and the manager is notified and watching.
2. Wait for the next job run within the cooldown: it does not fire again.
3. Set max fires to 2 and age the ticket repeatedly: it fires twice and then stops.
4. Create a no-agent-response rule at 120 minutes. Leave a ticket unanswered over a weekend: it does not fire until working hours resume — proving working-minute measurement.
5. Create a raise-priority rule on a ticket already at the highest priority: outcome is Skipped with the reason recorded, not an error.
6. Create a change-department rule: the ticket transfers and its SLA due time is unchanged.
7. Confirm every firing appears in ticket history attributed to the rule name, not to a user.
8. Deactivate a rule mid-cycle: it stops firing immediately and its history remains.
9. Run the tester on a broad rule: it lists matching tickets and fires nothing.
10. Create 1,000 at-risk tickets and confirm the job completes within its interval and logs the summary.

## Done Criteria

- [ ] All five triggers work, with idle time measured in working minutes.
- [ ] All six actions work, including the skip-with-reason case.
- [ ] Cooldown and fire caps are enforced from the decision log.
- [ ] Firings are attributed to the rule by name in ticket history.
- [ ] The job is non-overlapping, batched and logs pass summaries.
- [ ] The tester is genuinely read-only.
- [ ] The builder shows only the inputs relevant to the chosen trigger and action.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
