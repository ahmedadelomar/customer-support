# Story 12 — Unified interaction timeline (Story: CS-103-interaction-history)

## Prerequisites

- CS-101 must be complete.
- Timeline entries are written by other features. Until Section 3 lands, only ticket-sourced entries appear — that is expected.

## Story Goal

One reverse-chronological timeline per customer covering every channel, written as a projection inside
the same transaction as the event it describes, and paged by keyset so it stays fast at any depth.

## Context — Read These Files First

1. `.squad/stories/customer-management/CS-103-interaction-history/intake.md`.
2. [backend/src/CustomerSupport.Domain/Customers/Interaction.cs](backend/src/CustomerSupport.Domain/Customers/Interaction.cs) — note `SourceType` / `SourceId` are a loose reference so any aggregate can project onto the timeline.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/CustomerConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/CustomerConfigurations.cs) — the `(CustomerId, OccurredAt)` index the timeline query depends on.
4. [frontend/src/app/features/agent/customers/customer-detail.page.html](frontend/src/app/features/agent/customers/customer-detail.page.html) — the History tab placeholder to replace.

## Product rules (from story)

- **The timeline is a projection, never edited.** No create, update or delete endpoint exists for it.
- **Entries are written in the same transaction as their source.** A separate job would drift, and a drifted timeline is worse than none.
- **Writing an entry updates `Customer.LastInteractionAt`**, which the customer list sorts on.
- **Preview text is plain text and truncated at write time**, so rendering the timeline never has to parse HTML or load message bodies.
- **Keyset pagination on `(OccurredAt, Id)`.** Offset paging degrades with depth and can skip rows when new entries arrive mid-scroll.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/customers/{customerId}/interactions` | `customers.history.view` | Keyset paged: `?before={occurredAt}&beforeId={id}&pageSize=`. Filters: `channel`, `direction`, `from`, `to`. |

## Backend Tasks

### 1 — Recorder abstraction

**File:** `backend/src/CustomerSupport.Application/Common/Interfaces/IInteractionRecorder.cs`

```csharp
public interface IInteractionRecorder
{
    /// <summary>Adds a timeline entry to the current unit of work. The caller saves.</summary>
    void Record(Guid customerId, ChannelKey channel, MessageDirection direction,
                string? subject, string? preview, Guid? ticketId = null,
                string? sourceType = null, Guid? sourceId = null, Guid? agentId = null);
}
```

Mirror `TicketEventRecorder`: add to the change tracker, do not save. The caller's `SaveChangesAsync` commits the source row and the timeline entry together.

The implementation also sets `Customer.LastInteractionAt` and truncates `preview` to 1000 characters, stripping HTML first.

### 2 — Keyset-paged timeline query

**File:** `backend/src/CustomerSupport.Application/Customers/Queries/GetInteractionsQuery.cs`

```csharp
var query = db.Interactions.AsNoTracking()
    .Where(i => i.CustomerId == request.CustomerId);

if (request.Channel is not null) query = query.Where(i => i.Channel == request.Channel);
if (request.Direction is not null) query = query.Where(i => i.Direction == request.Direction);
if (request.From is not null) query = query.Where(i => i.OccurredAt >= request.From);
if (request.To is not null) query = query.Where(i => i.OccurredAt <= request.To);

// Keyset: everything strictly older than the last row of the previous page.
if (request.Before is { } before)
{
    query = query.Where(i =>
        i.OccurredAt < before ||
        (i.OccurredAt == before && i.Id < request.BeforeId));
}

var items = await query
    .OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id)
    .Take(request.PageSize + 1)   // one extra row tells us whether more exist
    .Select(...)
    .ToListAsync(ct);
```

Return `hasMore` from whether the extra row came back, and drop it from the payload.

### 3 — Call the recorder from every source

Wire `IInteractionRecorder.Record` into: ticket creation (CS-201), inbound and outbound ticket messages (Section 3), chat session end (CS-303), web form submission (CS-305), portal ticket submission (CS-801) and CSAT response (CS-805).

Each of those stories owns its own call; this story owns the abstraction, the query and the UI. Note the dependency explicitly in each of those plans so none is missed.

## Frontend Tasks

### 4 — Timeline component

**File:** `frontend/src/app/features/agent/customers/history/interaction-timeline.component.ts`

A vertical timeline with a channel icon per entry, direction arrow, relative timestamp with an absolute tooltip, subject, preview and agent name. Clicking an entry routes to the source ticket.

Load more with an `IntersectionObserver` sentinel at the bottom, passing the last entry's `occurredAt` and `id` as the keyset cursor. Keep the accumulated entries in a signal and append.

Group entries under sticky date headers ("Today", "Yesterday", then a formatted date in the active locale).

### 5 — Filters and tab wiring

Add channel, direction and date-range filters above the timeline, resetting the accumulated list and the cursor when any changes.

Replace the History tab placeholder in `customer-detail.page.html` with the component, gated on `customers.history.view`. Load lazily on first tab activation rather than with the profile, since most profile views never open it.

## Verification Steps

1. Create a ticket for a customer: a timeline entry appears with the correct channel and direction.
2. Reply to the ticket: an outbound entry appears, and `LastInteractionAt` updates.
3. Confirm the customer list sorted by last interaction reflects the change.
4. Seed 500 interactions and scroll: pages load smoothly with no duplicated or skipped entries.
5. Add a new interaction while scrolled deep, then continue scrolling: no row is skipped — this is the behaviour offset paging gets wrong.
6. Filter by channel and by date range: only matching entries show.
7. Click an entry: the source ticket opens.
8. Confirm no endpoint exists to create, edit or delete an interaction.
9. Confirm the timeline renders correctly in Arabic with RTL and locale-formatted dates.

## Done Criteria

- [ ] `IInteractionRecorder` exists and writes into the caller's unit of work.
- [ ] The timeline query uses keyset pagination and supports channel, direction and date filters.
- [ ] Previews are plain text and truncated at write time.
- [ ] `LastInteractionAt` is maintained.
- [ ] The timeline component loads incrementally with date grouping and links to sources.
- [ ] Every downstream feature plan records its obligation to call the recorder.
- [ ] No mutation endpoint exists for the timeline.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
