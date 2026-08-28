# Story 38 — Tracking and replying to requests (Story: CS-802-track-requests)

## Prerequisites

- CS-801 must be complete.
- CS-204 must be complete for status kinds and reopening behaviour.
- **Write portal-specific DTOs.** Do not reuse `TicketDetailDto`.

## Story Goal

Customers see their requests and can reply, without ever seeing anything internal. This story is
defined as much by what it excludes as by what it shows.

## Context — Read These Files First

1. `.squad/stories/customer-portal/CS-802-track-requests/intake.md`.
2. [backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs](backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs) — `IsInternalNote` is the only gate between an agent note and a customer seeing it.
3. [backend/src/CustomerSupport.Domain/Tickets/TicketStatus.cs](backend/src/CustomerSupport.Domain/Tickets/TicketStatus.cs) — `IsVisibleInPortal` and `Kind`.
4. [backend/src/CustomerSupport.Domain/Files/Attachment.cs](backend/src/CustomerSupport.Domain/Files/Attachment.cs) — `IsPublic` marks files a customer may download.
5. [backend/src/CustomerSupport.Domain/Sla/TicketSlaClock.cs](backend/src/CustomerSupport.Domain/Sla/TicketSlaClock.cs) — the customer sees an expectation, never the breach state.

## Product rules (from story)

- **Excluded from every portal response, without exception:** internal notes, mentions, ticket events other than status changes, assignee identity beyond a display name, SLA breach state, internal statuses, other customers' anything.
- **A status not visible in the portal renders as a neutral "In progress"** derived from its `Kind`, so administrators can add internal statuses freely.
- **A customer reply reopens a resolved ticket** (CS-204) and notifies the assignee.
- **Attachments are downloadable only when `IsPublic`**, re-checked at download.
- **Show an expectation, never a breach.** "We aim to reply by Sunday 11:00" is useful; "SLA breached" is an internal concern.
- **A ticket belonging to another customer returns 404**, never 403.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Portal-specific DTOs and queries

**File:** `backend/src/CustomerSupport.Application/Portal/Tickets/`

Define `PortalTicketListItemDto` and `PortalTicketDetailDto` from scratch. They must not inherit from or reuse the agent DTOs — inheritance means the next field added to the agent DTO silently appears in the portal.

Scope every query by the claim and filter internal content in the projection:

```csharp
var customerId = currentUser.CustomerId
    ?? throw new ForbiddenException("This endpoint is for portal users.");

var ticket = await db.Tickets
    .AsNoTracking()
    .Where(t => t.CustomerId == customerId)          // scoping BEFORE the id match, so cross-customer is 404
    .Select(t => new PortalTicketDetailDto
    {
        Number = t.Number, Subject = t.Subject,
        StatusLabel = t.Status.IsVisibleInPortal
            ? t.Status.Name
            : NeutralLabelFor(t.Status.Kind),        // internal statuses never leak
        Messages = t.Messages
            .Where(m => !m.IsInternalNote && !m.IsDeleted)   // the critical filter
            .OrderBy(m => m.SentAt)
            .Select(m => new PortalMessageDto { ... })
            .ToList(),
        ExpectedReplyBy = t.FirstRespondedAt == null ? t.FirstResponseDueAt : null,
        // Deliberately absent: IsResolutionBreached, EscalationLevel, AssignedAgentId, Events.
    })
    .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
    ?? throw new NotFoundException(nameof(Ticket), request.Id);
```

### 2 — Portal reply, close and attachment access

`AddPortalReplyCommand` verifies ownership, creates an inbound `TicketMessage` with `AuthorType = Customer`, sets `LastCustomerReplyAt`, increments `CustomerReplyCount`, records an `Interaction`, notifies the assignee, and triggers the CS-204 reopen path when the ticket is resolved.

`ClosePortalTicketCommand` moves the ticket to the default closed status with a system event noting the customer closed it.

Extend `IAttachmentOwnerAuthorizer` (CS-104) for portal callers: a portal user may access an attachment only when it hangs off their own ticket **and** `IsPublic` is true. Agent uploads default to public on customer-visible messages and non-public on internal notes.

## Frontend Tasks

### 3 — My requests list and detail

The list shows number, subject, status chip, last update and channel, with open/closed tabs and a search box. Card layout on mobile.

The detail view shows the conversation as chat bubbles — agent messages on one side, the customer's on the other — with attachment chips and dates in the customer's locale.

Above the conversation, when a first reply is still pending, show the expectation: "We aim to reply by {{date}}". Show nothing when it has already been answered, and never show breach state.

The reply box is disabled with an explanation on a closed ticket, offering **Reopen** where CS-803 allows it.

## Verification Steps

1. Sign in as a customer and open My requests: only their own tickets appear.
2. Request another customer's ticket by id: 404, not 403.
3. Add an internal note as an agent: confirm it is absent from the portal detail response, not merely hidden in the UI.
4. Mention a colleague in a note: confirm the mention is absent from the portal response.
5. Move a ticket to an internal-only status: the portal shows a neutral "In progress".
6. Confirm the portal detail response contains no breach flag, escalation level or assignee id.
7. Reply from the portal: the message appears in the agent thread and the assignee is notified.
8. Reply to a resolved ticket: it reopens and the SLA clock resumes.
9. Upload a file on an internal note and try to download it as the customer: refused.
10. Upload a file on a customer-visible reply: the customer can download it.
11. Close a ticket from the portal: a system event records that the customer closed it.
12. Inspect the raw portal API responses in the browser network tab for any internal field.

## Done Criteria

- [ ] Portal DTOs are defined independently and share no inheritance with agent DTOs.
- [ ] Internal notes, mentions, events, assignee identity and SLA breach state are absent from every portal response.
- [ ] Internal statuses render as a neutral label derived from `Kind`.
- [ ] Cross-customer access returns 404.
- [ ] Portal replies reopen resolved tickets and notify the assignee.
- [ ] Attachment access is re-checked for portal callers.
- [ ] A response expectation is shown without exposing SLA internals.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
