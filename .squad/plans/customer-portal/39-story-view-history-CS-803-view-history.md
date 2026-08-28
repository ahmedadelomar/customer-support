# Story 39 — Request history, reopening and self-service profile (Story: CS-803-view-history)

## Prerequisites

- CS-802 must be complete.
- CS-102 must be complete for contact verification, which profile edits reuse.

## Story Goal

Customers can look back at what happened, reopen a recent request rather than raising a duplicate, and
maintain their own details — with the same verification rules agents are held to.

## Context — Read These Files First

1. `.squad/stories/customer-portal/CS-803-view-history/intake.md`.
2. [backend/src/CustomerSupport.Application/Portal/Tickets/](backend/src/CustomerSupport.Application/Portal/Tickets/) (CS-802) — the portal DTOs to extend.
3. [backend/src/CustomerSupport.Application/Customers/Contacts/](backend/src/CustomerSupport.Application/Customers/Contacts/) (CS-102) — the verification flow to reuse.
4. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — `ClosedAt` drives the reopen window.

## Product rules (from story)

- **Reopen within the window; follow-up beyond it.** The window reuses `tickets.autoCloseResolvedAfterDays` semantics rather than a second setting.
- **A follow-up ticket links to the original**, so the history stays connected.
- **Profile edits are audited** exactly as agent-made changes are.
- **Changing a primary email or phone requires verification** before it takes effect — otherwise a compromised account could redirect all correspondence.
- **The summary download is generated server-side** in the customer's language, containing only customer-visible content.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — History query and reopen

`GetPortalHistoryQuery` extends the list query with date-range, status and category filters plus free-text search across number, subject and message bodies (customer-visible only).

`ReopenPortalTicketCommand`:

```csharp
var window = TimeSpan.FromDays(await settings.GetAsync("tickets.reopenWindowDays", 14, ct));

if (ticket.ClosedAt is { } closed && clock.UtcNow - closed <= window)
{
    ticket.StatusId = defaultOpenStatusId;
    ticket.ReopenCount += 1;
    ticket.ClosedAt = null;
    events.Record(ticket.Id, TicketEventType.Reopened, metadataJson: Json(new { request.Reason, by = "customer" }));
    await sla.OnStatusChangedAsync(ticket.Id, ct);   // resume, never restart
}
else
{
    // Beyond the window, a follow-up keeps resolution-time reporting honest.
    var followUpId = await mediator.Send(new CreateTicketCommand { ... }, ct);
    return new ReopenResult(FollowUpTicketId: followUpId);
}
```

The response tells the client which happened, so the UI can explain it rather than silently doing something different from what the button said.

### 2 — Self-service profile and summary export

`UpdatePortalProfileCommand` allows only display names, preferred language, preferred channel and time zone. Contact changes route through the CS-102 verification flow: a new email or phone is added unverified and becomes primary only after the code is confirmed.

`GET /api/portal/tickets/{id}/summary` renders a PDF (or a printable HTML page) containing the ticket number, dates, category, resolution note and the customer-visible conversation, in the customer's language with correct direction. Reuse the portal detail projection so it cannot include more than the screen does.

## Frontend Tasks

### 3 — History page and reopen flow

A history list with date-range, status and category filters and a search box, showing satisfaction score per closed ticket where one exists.

Closed tickets open read-only with a clear banner. Where the reopen window is open, a **Reopen** action asks for a reason; beyond it, the same place offers **Create a follow-up request**, pre-filled with a reference to the original — the wording must match what will actually happen.

### 4 — Profile page

Editable name, language, preferred channel and time zone, plus a contacts section listing email and phone with verified badges.

Adding or changing a contact opens the verification dialog from CS-102, reused rather than rebuilt. Make clear that the new address becomes primary only after verification.

## Verification Steps

1. Open History: past tickets appear with filters and search working.
2. Search for a word appearing only in an internal note: no match, confirming search covers only customer-visible content.
3. Open a closed ticket: read-only with the reply box disabled and explained.
4. Reopen a ticket closed 2 days ago: it returns to open, history intact, SLA resumed rather than restarted.
5. Attempt to reopen a ticket closed 60 days ago: the UI offers a follow-up instead, and creates one linked to the original.
6. Download a ticket summary: it contains only customer-visible content, in the right language and direction.
7. Update the display name from the portal: it saves and appears in the audit log.
8. Change the primary email: it is added unverified and does not become primary until the code is confirmed.
9. Confirm the old email remains primary until then.
10. Attempt to change a field not on the allowed list by posting it directly: ignored.

## Done Criteria

- [ ] History is searchable and filterable across customer-visible content only.
- [ ] Reopen within the window resumes the ticket; beyond it a linked follow-up is created, and the UI wording matches.
- [ ] Summary export contains only portal-visible content, in the customer's language.
- [ ] Profile edits are limited to the allowed fields and are audited.
- [ ] Contact changes reuse the CS-102 verification flow and do not take effect until verified.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
