# Story 16 — Manual assignment, claiming and bulk assign (Story: CS-203-assign-tickets-agents)

## Prerequisites

- CS-201 and CS-1203 must be complete — teams, capacity and rotation order come from the latter.
- CS-504 provides notification delivery; until it lands, write the `Notification` row and skip the fan-out.

## Story Goal

Every ticket gets an owner. Agents assign, reassign and claim; leaders bulk-assign from the list.
Capacity and availability are surfaced as warnings rather than hard blocks, because overriding them is
sometimes the right call.

The claim path is explicitly race-safe: two agents watching the same queue will click at the same moment.

## Context — Read These Files First

1. `.squad/stories/ticket-management/CS-203-assign-tickets-agents/intake.md`.
2. [backend/src/CustomerSupport.Domain/Organization/TeamMember.cs](backend/src/CustomerSupport.Domain/Organization/TeamMember.cs) — `MaxConcurrentTickets`, `RotationOrder`.
3. [backend/src/CustomerSupport.Infrastructure/Identity/ApplicationUser.cs](backend/src/CustomerSupport.Infrastructure/Identity/ApplicationUser.cs) — `AvailabilityStatus`, `MaxConcurrentTickets`.
4. [backend/src/CustomerSupport.Infrastructure/Services/TicketEventRecorder.cs](backend/src/CustomerSupport.Infrastructure/Services/TicketEventRecorder.cs).
5. CS-502, which must reuse the capacity rules defined here rather than inventing its own.

## Product rules (from story)

- **Assignment sets `AssignedAgentId` and `AssignedAt`** and appends an `Assigned` event carrying the agent display name.
- **Claiming is a conditional update, not read-then-write.** If another agent claimed first, return 409 with their name.
- **Capacity and availability produce a warning, not a block.** The caller may proceed with `force: true`.
- **Bulk assignment is per-ticket, not all-or-nothing.** Report which succeeded and which failed, and why.
- **Assigning to a team without an agent** leaves the ticket in that team's queue, unowned.
- **The assignee is notified**; the previous assignee is notified on reassignment.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/api/tickets/{id}/assign` | `tickets.assign` | Body: `{ agentId?, teamId?, force? }`. |
| POST | `/api/tickets/{id}/claim` | `tickets.assign` | Assigns to the caller; 409 if already taken. |
| POST | `/api/tickets/{id}/unassign` | `tickets.assign` | |
| POST | `/api/tickets/bulk-assign` | `tickets.assign` | Body: `{ ticketIds[], agentId?, teamId?, force? }`. Returns a per-ticket result. |
| GET | `/api/tickets/assignable-agents` | `tickets.assign` | Agents for the ticket's team/department with their current load and availability. |

## Backend Tasks

### 1 — Shared capacity check

**File:** `backend/src/CustomerSupport.Application/Tickets/Assignment/AgentCapacityService.cs`

One implementation, used by both manual assignment and CS-502. Two implementations would disagree and confuse agents.

```csharp
public record CapacityResult(bool IsAvailable, int OpenTickets, int? Cap, string? Warning);

public async Task<CapacityResult> CheckAsync(Guid agentId, Guid? teamId, CancellationToken ct)
{
    var openCount = await db.Tickets.CountAsync(
        t => t.AssignedAgentId == agentId && !t.Status.IsTerminal, ct);

    // The team-member cap wins when present; otherwise the user-level cap; zero means unlimited.
    var cap = await ResolveCapAsync(agentId, teamId, ct);
    var user = await db.Users.FirstAsync(u => u.Id == agentId, ct);

    if (!user.IsActive) return new(false, openCount, cap, "Agent is deactivated.");
    if (user.AvailabilityStatus is "Away" or "Offline")
        return new(false, openCount, cap, $"Agent is {user.AvailabilityStatus}.");
    if (cap is > 0 && openCount >= cap)
        return new(false, openCount, cap, $"Agent is at capacity ({openCount}/{cap}).");

    return new(true, openCount, cap, null);
}
```

A deactivated agent is a hard block; Away and at-capacity are warnings that `force: true` overrides.

### 2 — Race-safe claim

**File:** `backend/src/CustomerSupport.Application/Tickets/Commands/ClaimTicketCommand.cs`

```csharp
// Conditional update: only succeeds if the ticket is still unassigned.
var updated = await db.Tickets
    .Where(t => t.Id == request.TicketId && t.AssignedAgentId == null)
    .ExecuteUpdateAsync(s => s
        .SetProperty(t => t.AssignedAgentId, currentUser.UserId)
        .SetProperty(t => t.AssignedAt, clock.UtcNow), ct);

if (updated == 0)
{
    var holder = await db.Tickets
        .Where(t => t.Id == request.TicketId)
        .Select(t => t.AssignedAgentId)
        .FirstOrDefaultAsync(ct);

    throw new ConflictException(holder is null
        ? "This ticket is no longer available."
        : $"This ticket was already claimed by {await NameOf(holder.Value, ct)}.");
}
```

Read-then-write would let both agents succeed and one silently overwrite the other. Append the `Assigned` event only after the conditional update reports a row was changed.

### 3 — Bulk assignment with partial results

Process each ticket independently and collect outcomes:

```csharp
public record BulkAssignResult(Guid TicketId, string Number, bool Succeeded, string? Error);
```

Wrap each ticket in its own try/catch so one failure does not abandon the rest, and return HTTP 207-style content: `{ succeeded: n, failed: m, results: [...] }`. Cap the batch at 100 tickets.

## Frontend Tasks

### 4 — Assignment control on the detail screen

An assignee picker in the properties sidebar showing each candidate's current load and availability inline (`Sara — 12/25 · Available`). Away and at-capacity agents render dimmed with a warning icon; selecting one opens a confirm dialog that explains the warning and, on confirm, resends with `force: true`.

Add an **Unassign** action and, when unassigned, a prominent **Claim** button.

### 5 — Bulk assign from the list

Add row checkboxes and a select-all-on-page control to the data table (a new optional `selectable` input, so other lists can opt in later). A sticky action bar appears when anything is selected, offering **Assign** and showing the count.

After a bulk assign, show a summary: how many succeeded, and an expandable list of failures with reasons. Do not simply toast "done" when some failed.

## Verification Steps

1. Assign a ticket: `AssignedAt` is set, an `Assigned` event names the agent, and the agent is notified.
2. Reassign: the history shows both the previous and the new assignee, and both are notified.
3. Claim an unassigned ticket: it becomes yours.
4. Simulate the race — issue two concurrent claims for the same ticket: one succeeds, the other gets 409 naming the winner, and only one `Assigned` event exists.
5. Assign to an Away agent: warned, and proceeding requires confirmation.
6. Assign to an agent at their cap: warned with the current count and cap.
7. Assign to a deactivated agent: refused outright, even with `force`.
8. Bulk assign 10 tickets where 2 will fail: the response reports 8 succeeded and 2 failed with reasons, and the 8 are genuinely assigned.
9. As a user without `tickets.assign`, confirm the controls are hidden and the endpoints return 403.

## Done Criteria

- [ ] `AgentCapacityService` exists and is the single capacity rule, ready for CS-502 to reuse.
- [ ] Claiming uses a conditional update and reports the winner on conflict.
- [ ] Availability and capacity warn and are overridable; deactivated agents are a hard block.
- [ ] Bulk assignment returns per-ticket results and is capped.
- [ ] Every assignment change appends an event and notifies the affected agents.
- [ ] The UI shows load and availability inline and surfaces partial bulk failures honestly.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
