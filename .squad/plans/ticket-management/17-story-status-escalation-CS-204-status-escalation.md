# Story 17 — Status workflow, resolution, reopening and manual escalation (Story: CS-204-status-escalation)

## Prerequisites

- CS-201 and CS-202 must be complete.
- **Agree the SLA pause/resume contract with CS-501 before starting.** This story pauses and resumes clocks; CS-501 owns their calculation. Implementing them with different assumptions produces wrong breach reports.

## Story Goal

A ticket's status is always meaningful. Statuses are configurable but map to fixed kinds, so renaming
never changes behaviour. Resolution requires a note and queues a survey, pending statuses pause the SLA
clock, reopening resumes rather than restarts it, and stale resolved tickets close themselves.

## Context — Read These Files First

1. `.squad/stories/ticket-management/CS-204-status-escalation/intake.md`.
2. [backend/src/CustomerSupport.Domain/Tickets/TicketStatus.cs](backend/src/CustomerSupport.Domain/Tickets/TicketStatus.cs) — read the remark on `Kind`: it is what keeps behaviour predictable when administrators rename statuses.
3. [backend/src/CustomerSupport.Domain/Enums/TicketEnums.cs](backend/src/CustomerSupport.Domain/Enums/TicketEnums.cs) — `TicketStatusKind`.
4. [backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs](backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs) — `ISlaEngine.OnStatusChangedAsync` and `OnResolvedAsync` are the hooks to call.
5. [backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs) — the seven seeded statuses and their flags.

## Product rules (from story)

- **Behaviour follows `Kind`, never the name or code.** Any code branching on a status name is a defect.
- **Resolving requires a resolution note**, sets `ResolvedAt` / `ResolvedById`, and queues a CSAT survey (CS-805).
- **A `PausesSla` status pauses the resolution clock; the first-response clock is unaffected** — waiting on the customer does not excuse never having replied.
- **A customer reply to a resolved (non-terminal) ticket reopens it**, increments `ReopenCount`, and **resumes** the SLA clock. Restarting it would erase an existing breach.
- **Terminal statuses make a ticket read-only** except for reopening.
- **Auto-close** runs on resolved tickets idle for `tickets.autoCloseResolvedAfterDays`, recording a system-generated `Closed` event.
- **Manual escalation requires a reason**, increments `EscalationLevel`, and notifies the department manager and watchers.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Change-status command with kind-driven side effects

**File:** `backend/src/CustomerSupport.Application/Tickets/Commands/ChangeTicketStatusCommand.cs`

```csharp
var oldStatus = ticket.Status;
var newStatus = await db.TicketStatuses.FirstAsync(s => s.Id == request.StatusId, ct);

if (newStatus.Kind == TicketStatusKind.Resolved && string.IsNullOrWhiteSpace(request.ResolutionNote))
{
    throw new ValidationException(new Dictionary<string, string[]>
    {
        ["resolutionNote"] = ["A resolution note is required when resolving a ticket."],
    });
}

ticket.StatusId = newStatus.Id;

switch (newStatus.Kind)
{
    case TicketStatusKind.Resolved:
        ticket.ResolvedAt = clock.UtcNow;
        ticket.ResolvedById = currentUser.UserId;
        ticket.ResolutionNote = request.ResolutionNote;
        break;
    case TicketStatusKind.Closed or TicketStatusKind.Cancelled:
        ticket.ClosedAt = clock.UtcNow;
        break;
}

events.Record(ticket.Id, TicketEventType.StatusChanged,
    field: nameof(Ticket.StatusId),
    oldValue: oldStatus.Id.ToString(), newValue: newStatus.Id.ToString(),
    oldDisplay: oldStatus.Name.For(lang), newDisplay: newStatus.Name.For(lang));

await db.SaveChangesAsync(ct);

// After the save, so the engine reads committed state.
await sla.OnStatusChangedAsync(ticket.Id, ct);
if (newStatus.Kind == TicketStatusKind.Resolved) await sla.OnResolvedAsync(ticket.Id, ct);
```

Capture display values **before** mutating, or the history records the new name twice.

### 2 — Reopen on customer reply

In the inbound-message path (used by both the portal and Section 3 channels): if the ticket's current status kind is `Resolved`, move it to the default open status, increment `ReopenCount`, clear `ResolvedAt`, record a `Reopened` event, and call `ISlaEngine.OnStatusChangedAsync` so the clock **resumes**.

If the kind is `Closed` or `Cancelled`, do **not** reopen — create a linked follow-up ticket instead and record the link. Reopening a closed ticket weeks later distorts resolution-time reporting.

### 3 — Auto-close job and manual escalation

`AutoCloseResolvedTicketsJob` (Quartz, hourly): find tickets whose status kind is `Resolved` and whose `ResolvedAt` is older than the configured window with no later customer reply, move them to the default closed status, and record a system-generated `Closed` event with `TriggeredByRule = "auto-close"`. Batch in chunks of 500.

`EscalateTicketCommand` requires a reason, increments `EscalationLevel`, sets `EscalatedAt`, records an `Escalated` event with the reason in `MetadataJson`, adds the department manager as a watcher, and notifies them plus existing watchers.

### 4 — Status administration

CRUD following CS-202. Guard the invariants: exactly one `IsDefault`; at least one status of kind `Closed`; deletion refused when tickets reference the status. Changing `Kind` on a status already in use should warn loudly — it silently changes SLA and reporting behaviour for every ticket in that status.

## Frontend Tasks

### 5 — Status control and resolve dialog

The status picker in the properties sidebar groups options by kind. Selecting a Resolved-kind status opens a dialog requiring a resolution note, with a hint that a satisfaction survey will be sent.

When the ticket is in a terminal status, disable the composer and property editors and show a banner with a **Reopen** action where permitted.

### 6 — Escalation dialog and status admin

The escalate action takes a required reason and shows who will be notified. Display the current escalation level as a badge on the ticket header once above zero.

The status admin screen mirrors the priority editor: drag to reorder, kind selector, colour, and the terminal / pauses-SLA / portal-visible / default toggles. Show a prominent warning when changing the kind of a status that tickets currently use.

## Verification Steps

1. Resolve a ticket without a note: rejected with a field-level error. With a note: `ResolvedAt` and `ResolvedById` are set and a survey is queued.
2. Move a ticket to Pending Customer: the resolution clock pauses and the first-response clock does not.
3. Return it to Open: the resolution clock resumes from where it paused, not from zero.
4. Have a customer reply to a resolved ticket: it reopens, `ReopenCount` becomes 1, and the SLA due time is unchanged from before resolution.
5. Have a customer reply to a closed ticket: a linked follow-up ticket is created instead of reopening.
6. Rename "Pending Customer" to something else and confirm SLA pausing still works — this proves behaviour follows `Kind`.
7. Set `tickets.autoCloseResolvedAfterDays` to 0.001 days, run the job, and confirm resolved tickets close with a system-generated event.
8. Escalate a ticket without a reason: rejected. With one: the level increments and the manager is notified and watching.
9. Confirm a terminal ticket is read-only apart from reopening.
10. Attempt to delete a status in use: refused.

## Done Criteria

- [ ] All status side effects are driven by `Kind`, never by name or code.
- [ ] Resolution requires a note and queues a survey.
- [ ] Pausing affects only the resolution clock; reopening resumes rather than restarts.
- [ ] Replies to closed tickets create follow-ups instead of reopening.
- [ ] The auto-close job works, is batched and records system-generated events.
- [ ] Manual escalation requires a reason and notifies the manager and watchers.
- [ ] Status admin enforces the default and closed-kind invariants and warns on `Kind` changes.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
