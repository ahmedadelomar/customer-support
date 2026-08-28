# Story 18 — Ticket history timeline (Story: CS-205-ticket-history)

## Prerequisites

- CS-201 must be complete; ideally 15–17 as well, so there are varied events to render.
- `TicketEventRecorder` already exists. This story builds the reader and the UI, and audits that every mutation actually records.

## Story Goal

A readable, complete, tamper-proof record of a ticket's life. Events render as sentences in the active
language, display values survive later renames, automation is clearly attributed, and events interleave with
messages on one timeline.

## Context — Read These Files First

1. `.squad/stories/ticket-management/CS-205-ticket-history/intake.md`.
2. [backend/src/CustomerSupport.Domain/Tickets/TicketEvent.cs](backend/src/CustomerSupport.Domain/Tickets/TicketEvent.cs) — note the remark explaining why display values are captured at write time.
3. [backend/src/CustomerSupport.Domain/Enums/TicketEnums.cs](backend/src/CustomerSupport.Domain/Enums/TicketEnums.cs) — the full `TicketEventType` list, which the renderer must cover exhaustively.
4. [backend/src/CustomerSupport.Infrastructure/Services/TicketEventRecorder.cs](backend/src/CustomerSupport.Infrastructure/Services/TicketEventRecorder.cs) — note that a null `ActorId` marks the event system-generated.
5. [frontend/src/app/features/agent/customers/history/interaction-timeline.component.ts](frontend/src/app/features/agent/customers/history/interaction-timeline.component.ts) (CS-103) — the keyset-paged timeline pattern to reuse.

## Product rules (from story)

- **Append-only.** No update or delete endpoint exists.
- **Display values are rendered from what was captured at write time**, never re-resolved from current lookup rows.
- **Sentences come from parameterised translation keys**, never string concatenation — concatenated fragments cannot be ordered correctly in Arabic.
- **Automation is attributed by rule name**, not shown as an anonymous system action.
- **The system-events toggle defaults to off**, because routine automation drowns the human actions agents are looking for.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/tickets/{id}/history` | `tickets.history.view` | Keyset paged. Filters: `eventTypes[]`, `includeSystem`. |
| GET | `/api/tickets/{id}/timeline` | `tickets.view` | Events and messages merged, chronological. |

## Backend Tasks

### 1 — History and merged timeline queries

**File:** `backend/src/CustomerSupport.Application/Tickets/Queries/`

`GetTicketHistoryQuery` uses keyset pagination on `(OccurredAt, Id)` like CS-103, filters by event type, and excludes `IsSystemGenerated` unless `includeSystem` is set.

`GetTicketTimelineQuery` merges events and messages. Do the merge in the database with a union projected to a common shape rather than fetching both fully and merging in memory — a long ticket has thousands of rows:

```csharp
var events = db.TicketEvents.Where(e => e.TicketId == id)
    .Select(e => new TimelineEntryDto { Kind = "event", OccurredAt = e.OccurredAt, ... });

var messages = db.TicketMessages.Where(m => m.TicketId == id && !m.IsDeleted)
    .Select(m => new TimelineEntryDto { Kind = "message", OccurredAt = m.SentAt, ... });

var page = await events.Concat(messages)
    .OrderByDescending(x => x.OccurredAt)
    .Take(pageSize + 1)
    .ToListAsync(ct);
```

Portal callers must never receive entries where `IsInternalNote` is true.

### 2 — Audit that every mutation records

**File:** `backend/tests/CustomerSupport.UnitTests/Tickets/TicketHistoryCompletenessTests.cs`

The invariant "every mutation appends an event" is only real if it is tested. Write an integration test that, for each ticket command (`CreateTicket`, `ChangeStatus`, `ChangePriority`, `ChangeCategory`, `Assign`, `Unassign`, `Escalate`, `Reply`, `AddInternalNote`, `Transfer`, `Merge`), executes the command against an in-memory context and asserts that the expected `TicketEventType` was appended and that its display values are populated.

This test is the reason the invariant will still hold in a year.

## Frontend Tasks

### 3 — Event sentence renderer

**File:** `frontend/src/app/features/agent/tickets/history/ticket-event.pipe.ts`

Map each `TicketEventType` to a parameterised translation key:

```json
"tickets.history.events": {
  "1": "{{actor}} changed status from {{from}} to {{to}}",
  "2": "{{actor}} changed priority from {{from}} to {{to}}",
  "4": "{{actor}} assigned this ticket to {{to}}",
  "6": "{{actor}} escalated this ticket to level {{level}}",
  "10": "SLA {{target}} was breached"
}
```

and in Arabic:

```json
"tickets.history.events": {
  "1": "قام {{actor}} بتغيير الحالة من {{from}} إلى {{to}}",
  "2": "قام {{actor}} بتغيير الأولوية من {{from}} إلى {{to}}",
  "4": "قام {{actor}} بإسناد التذكرة إلى {{to}}",
  "6": "قام {{actor}} بتصعيد التذكرة إلى المستوى {{level}}",
  "10": "تم تجاوز اتفاقية مستوى الخدمة لـ {{target}}"
}
```

Parameterised keys let each language order the sentence naturally; concatenation cannot. Use `newDisplayValue` and `oldDisplayValue` from the event, never a fresh lookup.

For system-generated events, substitute the rule name for `{{actor}}`, or a translated "System" when no rule is named.

### 4 — History tab

Reuse the keyset timeline pattern from CS-103. Group under sticky date headers, show an icon per event type, and render the sentence with a relative timestamp and an absolute tooltip.

Add an event-type multi-select filter and a **Show system events** toggle, defaulting to off. Show the count of hidden system events so the toggle is discoverable rather than a secret.

On the Conversation tab, interleave events inline between messages as slim single-line entries, so the thread reads as a narrative rather than requiring a tab switch to understand what happened.

## Verification Steps

1. Perform each mutation type on a ticket and confirm each appears in history with a correct sentence.
2. Switch language and confirm every sentence reads naturally in Arabic, with correct word order.
3. Rename a status, then reload history: the old entry still shows the original name.
4. Trigger an automation (an escalation rule) and confirm the entry names the rule rather than an anonymous system actor.
5. Toggle system events off: automation entries hide and the hidden count is shown.
6. Filter by event type: only matching entries appear.
7. Seed 2,000 events and scroll: pages load smoothly with no duplicates or skips.
8. Confirm the merged timeline interleaves messages and events in true chronological order.
9. Confirm no portal endpoint returns internal notes on the timeline.
10. Confirm no API can modify or delete a history entry.
11. Run the history completeness test: every ticket command records its event.

## Done Criteria

- [ ] History and merged-timeline endpoints exist, keyset-paged and filtered.
- [ ] Sentences render from parameterised translation keys in both languages.
- [ ] Display values come from the event, so history survives renames.
- [ ] Automation is attributed by rule name.
- [ ] The system-events toggle defaults off and shows the hidden count.
- [ ] The completeness test asserts every ticket command records an event.
- [ ] No mutation endpoint exists for history, and internal notes never reach the portal.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
