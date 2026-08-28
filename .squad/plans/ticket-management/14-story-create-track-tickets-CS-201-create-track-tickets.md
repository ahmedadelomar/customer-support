# Story 14 — Ticket creation, list and detail screen (Story: CS-201-create-track-tickets)

## Prerequisites

- CS-1001 (auth), CS-101 (customers) and CS-1203 (departments) must be complete.
- The `TicketNumbers` sequence must exist — it is created in the CS-1001 migration. Without it `ReferenceNumberGenerator` throws at runtime.
- Read the customers slice first. This story follows exactly the same list/detail/form structure.

## Story Goal

Agents can raise, find, read and reply to tickets. This story delivers the ticket list, the ticket
detail screen with its conversation thread, and the create form — the screens agents spend their day in.

It also establishes the two invariants the rest of the product depends on: every mutation appends a
`TicketEvent`, and every customer-visible exchange appends an `Interaction`.

## Context — Read These Files First

1. `.squad/stories/ticket-management/CS-201-create-track-tickets/intake.md`.
2. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — read the whole entity. Note the denormalised SLA columns and the comment explaining they mirror `TicketSlaClock`.
3. [backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs](backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs) — `IsInternalNote`, `ExternalMessageId` (unique, for idempotent ingestion), `AiSuggestionId`.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs) — the indexes define which queries are fast. The agent-queue index is `(AssignedAgentId, StatusId, CreatedAt)`.
5. [backend/src/CustomerSupport.Infrastructure/Services/TicketEventRecorder.cs](backend/src/CustomerSupport.Infrastructure/Services/TicketEventRecorder.cs) — adds to the unit of work; **the caller saves**.
6. [backend/src/CustomerSupport.Application/Customers/Queries/GetCustomersQuery.cs](backend/src/CustomerSupport.Application/Customers/Queries/GetCustomersQuery.cs) — the list-handler pattern to mirror.
7. [frontend/src/app/features/agent/customers/customer-list.page.ts](frontend/src/app/features/agent/customers/customer-list.page.ts) — the list-page pattern to mirror.
8. [frontend/src/app/features/agent/tickets/tickets.routes.ts](frontend/src/app/features/agent/tickets/tickets.routes.ts) — the stub route file to replace.

## Product rules (from story)

- **Number format `TCK-{year}-{seq:D6}`**, from the `TicketNumbers` sequence. Immutable, never reused.
- **Department resolution order:** category default, then channel-account default, then the system default. Never null.
- **A blocked customer cannot have a new ticket raised**, by an agent or through any channel.
- **Creation appends a `Created` event and an inbound `Interaction`.** In the same transaction.
- **An agent reply moves the ticket out of `New`** and sets `LastAgentReplyAt`; a customer reply sets `LastCustomerReplyAt` and increments `CustomerReplyCount`.
- **Internal notes are never sent outbound and never appear in the portal.** `IsInternalNote` is the only gate.
- **Merging moves messages to the target**, marks the source `MergedIntoTicketId`, makes it read-only, and records the merge in both histories.
- **All list state lives in the URL.**

## Data model

`Ticket`, `TicketMessage`, `TicketEvent`, `Tag`, `TicketTag` and `TicketWatcher` all already exist. Add one entity for saved views:

```csharp
// backend/src/CustomerSupport.Domain/Tickets/SavedTicketView.cs
public class SavedTicketView : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid OwnerId { get; set; }
    public LocalizedText Name { get; set; } = new();
    /// <summary>The filter set as JSON, matching the list query parameters.</summary>
    public string FiltersJson { get; set; } = "{}";
    public int DisplayOrder { get; set; }
    /// <summary>Shared views are visible to the owner's whole team.</summary>
    public bool IsShared { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
```

Index `(OwnerId, DisplayOrder)`.

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/tickets` | `tickets.view` | Paged. Filters: `assignment` (mine/team/unassigned/all), `statusId`, `statusKind`, `priorityId`, `categoryId`, `channel`, `departmentId`, `customerId`, `slaState`, `from`, `to`, `tag`. |
| GET | `/api/tickets/statistics` | `tickets.view` | Counts for the KPI tiles, scoped like the list. |
| GET | `/api/tickets/{id}` | `tickets.view` | Ticket, customer summary and properties. |
| GET | `/api/tickets/{id}/messages` | `tickets.view` | Paged, oldest first. Internal notes omitted for portal callers. |
| POST | `/api/tickets` | `tickets.create` | |
| PUT | `/api/tickets/{id}` | `tickets.update` | Subject, description, category, priority, tags. |
| POST | `/api/tickets/{id}/reply` | `tickets.reply` | Outbound message to the customer. |
| POST | `/api/tickets/{id}/note` | `tickets.note.internal` | Internal note. |
| POST | `/api/tickets/{id}/merge` | `tickets.merge` | Body: `{ targetTicketId, reason }`. |
| GET | `/api/tickets/views` , POST, DELETE | `tickets.view` | Saved filter views, per user. |

## Backend Tasks

### 1 — Ticket list query

**File:** `backend/src/CustomerSupport.Application/Tickets/Queries/GetTicketsQuery.cs`

Follow `GetCustomersQuery` exactly, then add the two scoping calls that matter:

```csharp
var query = db.Tickets.AsNoTracking()
    .WhereBranchAccessible(currentUser)   // platform 08
    .WhereTicketVisible(currentUser);     // platform 07 — department scoping
```

The `assignment` filter is the one agents use constantly:

```csharp
query = request.Assignment switch
{
    "mine"       => query.Where(t => t.AssignedAgentId == currentUser.UserId),
    "team"       => query.Where(t => t.AssignedTeamId != null && teamIds.Contains(t.AssignedTeamId.Value)),
    "unassigned" => query.Where(t => t.AssignedAgentId == null),
    _            => query,
};
```

Sortable allow-list: `number`, `subject`, `createdat`, `prioritylevel`, `resolutionduedat`, `lastcustomerreplyat`. Default to `createdat` descending.

Project to a `TicketListItemDto` carrying both languages for category, priority and status, plus the customer display name and the SLA due times — the list must not need a second request to render.

### 2 — Create ticket command

**File:** `backend/src/CustomerSupport.Application/Tickets/Commands/CreateTicketCommand.cs`

```csharp
var customer = await db.Customers
    .WhereBranchAccessible(currentUser)
    .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
    ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

if (customer.IsBlocked)
{
    throw new ConflictException(
        $"Customer {customer.Code} is blocked and cannot raise new tickets. Reason: {customer.BlockedReason}");
}

var category = await db.TicketCategories.FirstAsync(c => c.Id == request.CategoryId, ct);
var status = await db.TicketStatuses.FirstAsync(s => s.IsDefault, ct);

var ticket = new Ticket
{
    Number = await numbers.NextTicketNumberAsync(ct),
    CustomerId = customer.Id,
    BranchId = customer.BranchId ?? currentUser.BranchId,
    Subject = request.Subject,
    Description = request.Description,
    Language = customer.PreferredLanguage,
    CategoryId = category.Id,
    PriorityId = request.PriorityId ?? category.DefaultPriorityId ?? defaultPriorityId,
    StatusId = status.Id,
    Channel = request.Channel,
    DepartmentId = request.DepartmentId ?? category.DefaultDepartmentId ?? defaultDepartmentId,
};

db.Tickets.Add(ticket);

events.Record(ticket.Id, TicketEventType.Created);
interactions.Record(customer.Id, request.Channel, MessageDirection.Inbound,
    ticket.Subject, Truncate(ticket.Description), ticket.Id, nameof(Ticket), ticket.Id);

await db.SaveChangesAsync(ct);   // ticket, event and interaction commit together
```

The description also becomes the first `TicketMessage` (inbound, from the customer), so the thread reads correctly from the top.

### 3 — Reply, internal note and merge

`ReplyToTicketCommand` creates an outbound `TicketMessage`, sets `LastAgentReplyAt`, moves the status out of `New` if it is there, records a `MessageAdded` event and an outbound `Interaction`, and calls `ISlaEngine.OnFirstAgentReplyAsync` (a no-op until CS-501 lands — add the call now so CS-501 only has to implement it).

`AddInternalNoteCommand` is the same minus the outbound side effects, with `IsInternalNote = true` and an `InternalNoteAdded` event. It must **not** write an `Interaction`: nothing was exchanged with the customer.

`MergeTicketsCommand` moves messages with `ExecuteUpdateAsync`, sets `MergedIntoTicketId`, moves the source to a terminal status, and records a `Merged` event on both tickets naming the other. Refuse merging a ticket into itself, and refuse merging across customers without an explicit override flag — a cross-customer merge is almost always a mistake.

## Frontend Tasks

### 4 — Ticket list page

**File:** `frontend/src/app/features/agent/tickets/ticket-list.page.ts`

Mirror `customer-list.page.ts`. Columns: number, subject, customer, category (localized), priority (chip coloured from `colorHex`), status (chip), assignee, SLA due (relative, red when past, amber within the warning threshold), created.

Above the table, an assignment segmented control — **My tickets / My team / Unassigned / All** — which is the filter agents change most and deserves to be one click rather than a dropdown.

KPI tiles from `/api/tickets/statistics`: open, unassigned, due today, breached.

### 5 — Ticket detail screen

**File:** `frontend/src/app/features/agent/tickets/ticket-detail.page.ts`

Three columns on desktop, stacked on mobile:

- **Left, ~20%:** customer panel — name, code, tier, contact chips, open ticket count, pinned notes (CS-104), link to the profile.
- **Centre:** the conversation thread, oldest first. Inbound left-aligned, outbound right-aligned, internal notes full-width on an amber background with a lock icon so they are unmistakable. Attachment chips per message. The composer at the bottom has **Reply** and **Internal note** tabs — a visibly different composer per mode is what stops an internal note being sent to a customer by accident.
- **Right, ~25%:** properties — status, priority, category, assignee, department, SLA due times, tags, watchers. Each editable inline, each writing through its own endpoint.

Tabs above the thread: **Conversation**, **History** (CS-205), **Related**.

### 6 — Create form and saved views

The create form takes customer (a typeahead against `/api/customers`, with an inline "create new customer" path), subject, description, category (a tree picker), priority, department and channel, plus attachments via the shared upload component.

Saved views render as chips above the assignment control. Saving captures the current query params; selecting one applies them. Store per user via the saved-views endpoints.

## Verification Steps

1. Create a ticket: the number matches `TCK-{year}-{6 digits}`, and creating two concurrently produces different numbers.
2. Confirm a `Created` event and an inbound `Interaction` both exist, and that the customer timeline shows the ticket.
3. Create a ticket in a category with a default priority and department: both are applied without being specified.
4. Try to create a ticket for a blocked customer: refused with the block reason.
5. Reply to a ticket: the message appears outbound, `LastAgentReplyAt` is set, and the status leaves New.
6. Add an internal note: it is visually distinct, and confirm no portal endpoint returns it.
7. Use each assignment filter and confirm the result set matches.
8. As an agent without `tickets.view.all`, confirm only your department's tickets and your own assignments appear.
9. Save a view, navigate away, return and apply it: the filters are restored.
10. Merge ticket B into A: messages move, B becomes read-only, and both histories record it.
11. Attempt to merge a ticket into itself: refused.
12. Confirm the list renders correctly in Arabic with RTL and translated chips.

## Done Criteria

- [ ] Ticket CRUD, reply, internal note and merge all work behind their permissions.
- [ ] Every mutation appends a `TicketEvent`; customer-visible exchanges append an `Interaction`.
- [ ] Branch and department scoping are applied to every ticket query.
- [ ] Blocked customers cannot have tickets raised.
- [ ] The list has the assignment control, the documented filters, URL-persisted state and saved views.
- [ ] The detail screen has the three-panel layout with a mode-distinct composer.
- [ ] `ISlaEngine.OnFirstAgentReplyAsync` is called on reply, ready for CS-501.
- [ ] Fully translated and responsive.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
