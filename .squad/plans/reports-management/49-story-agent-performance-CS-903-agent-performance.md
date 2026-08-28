# Story 49 — Agent performance and workload (Story: CS-903-agent-performance)

## Prerequisites

- CS-901 must be complete; CS-805 and CS-701 provide satisfaction and AI acceptance figures.
- **Visibility is permission-scoped.** An agent must not be able to enumerate colleagues by calling the endpoint directly.

## Story Goal

Leaders see how their team is doing, with quality shown beside volume so the report does not reward
closing tickets over solving problems. Agents see themselves.

## Context — Read These Files First

1. `.squad/stories/reports-management/CS-903-agent-performance/intake.md`.
2. [backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs](backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs) — `AgentId` is a dimension, so per-agent figures come straight from the rollup.
3. [backend/src/CustomerSupport.Application/Common/Security/Permissions.cs](backend/src/CustomerSupport.Application/Common/Security/Permissions.cs) — `Reports.ViewAgentPerformance`.
4. [backend/src/CustomerSupport.Application/Tickets/Assignment/AgentCapacityService.cs](backend/src/CustomerSupport.Application/Tickets/Assignment/AgentCapacityService.cs) (CS-203) — capacity for the workload view.

## Product rules (from story)

- **Without the permission, an agent sees only themselves** — enforced in the handler, not the UI.
- **Quality beside volume, always.** Reopen rate and escalation rate appear on the same row as resolved count.
- **Low-sample metrics are flagged**, and the flag is in the API response so the UI cannot forget it.
- **Availability is accounted for.** An agent away for three weeks should not read as underperforming.
- **No composite score.** The product does not compute an overall performance rating; the numbers are shown and a human interprets them.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Scoped agent report

Scope in the handler:

```csharp
IQueryable<TicketDailyMetric> query = db.TicketDailyMetrics.Where(m => m.AgentId != null);

if (!currentUser.HasPermission(Permissions.Reports.ViewAgentPerformance))
{
    // Agents see only themselves — never a colleague's figures.
    query = query.Where(m => m.AgentId == currentUser.UserId);
}
else if (!currentUser.HasPermission(Permissions.Tickets.ViewAll))
{
    var teamMemberIds = await GetTeamMemberIdsAsync(currentUser.UserId, ct);
    query = query.Where(m => teamMemberIds.Contains(m.AgentId!.Value));
}
```

Per agent: assigned, resolved, reopened, escalated, average first response and resolution (sums over counts), SLA compliance, CSAT average with response count, reopen rate, and AI suggestion acceptance rate.

Flag low samples in the DTO:

```csharp
IsLowConfidence = row.ResolvedCount < 10,   // surfaced in the API, not left to the client
```

Include working days in the period, derived from the agent's availability history, so figures can be shown per working day where that is fairer.

### 2 — Workload distribution

Current open tickets per agent against their resolved capacity from `AgentCapacityService`, with a utilisation percentage. Include agents with zero open tickets — an idle agent is exactly what this view exists to reveal, and omitting empty rows hides it.

## Frontend Tasks

### 3 — Agent performance and workload views

A table with one row per agent, sortable by any column, with quality columns (reopen rate, escalation rate) adjacent to volume columns rather than at the far right where they are ignored.

Low-confidence figures render greyed with a tooltip explaining the sample size.

A workload view as a horizontal bar per agent showing open tickets against capacity, coloured by utilisation, sorted by utilisation descending.

Every figure drills through to the tickets behind it.

## Verification Steps

1. Sign in as an agent without the permission: only their own row is returned, including when calling the API directly with another agent id.
2. Sign in as a team leader: their team members appear, and no one else.
3. Sign in as a manager with `tickets.view.all`: the whole department appears.
4. Verify one agent's resolved count against the ticket list.
5. Confirm reopen rate and escalation rate appear beside volume.
6. Create an agent with 3 resolved tickets: their metrics are flagged low-confidence in both the API response and the UI.
7. Confirm an agent who was away shows correct per-working-day figures.
8. Open the workload view: open tickets against capacity are correct, and idle agents appear.
9. Confirm no composite performance score is produced anywhere.

## Done Criteria

- [ ] Visibility is scoped in the handler by permission and team membership.
- [ ] Quality indicators appear beside volume.
- [ ] Low-sample metrics are flagged in the API and rendered distinctly.
- [ ] Availability is accounted for in per-working-day figures.
- [ ] Workload distribution includes idle agents.
- [ ] No composite score exists.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
