# Story 25 — Customer panel on the ticket screen (Story: CS-402-customer-information)

## Prerequisites

- CS-201 (ticket detail screen), CS-101, CS-102 and CS-104 must be complete.

## Story Goal

The agent never needs a second screen to know who they are helping. Identity, contacts, other open
tickets and pinned notes sit beside the conversation, with inline editing for the fields agents correct most.

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/CS-402-customer-information/intake.md`.
2. [frontend/src/app/features/agent/customers/customer-detail.page.html](frontend/src/app/features/agent/customers/customer-detail.page.html) — the identity header block to condense into a panel.
3. [backend/src/CustomerSupport.Application/Customers/Dtos/CustomerDtos.cs](backend/src/CustomerSupport.Application/Customers/Dtos/CustomerDtos.cs) — `CustomerDetailDto` already carries the header counts.
4. [backend/src/CustomerSupport.Domain/Customers/CustomerContact.cs](backend/src/CustomerSupport.Domain/Customers/CustomerContact.cs) — `AllowNotifications` drives the opted-out marker.

## Product rules (from story)

- **The panel loads with the ticket**, in the same response. A second round trip makes the ticket screen feel slow.
- **Other open tickets are shown prominently** — several open tickets from one customer usually means a duplicate.
- **A blocked customer produces a prominent warning with the reason.**
- **Inline edits are audited** like any other customer change.
- **Without `customers.view`, only the display name is shown.** The panel degrades rather than erroring.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Extend the ticket detail response

**File:** `backend/src/CustomerSupport.Application/Tickets/Queries/GetTicketByIdQuery.cs`

Add a `CustomerPanelDto` to the ticket detail projection: identity, tier, blocked state and reason, satisfaction score, last interaction, contacts, pinned notes, and other open tickets (id, number, subject, status, capped at 5 with a total count).

Populate it only when the caller holds `customers.view`; otherwise return just the display name. Doing this in the projection rather than the controller means no future caller can bypass it.

### 2 — Inline patch endpoint

`PATCH /api/customers/{id}/inline` accepting only the fields agents legitimately correct in flow: display names, preferred language, preferred channel, tier. Everything else stays on the full customer form.

Reuse `UpdateCustomerCommand`'s validation for the fields it touches rather than writing a second set of rules.

## Frontend Tasks

### 3 — Customer panel component

**File:** `frontend/src/app/features/agent/tickets/panels/customer-panel.component.ts`

Sections in order of what an agent needs first: identity with status and tier chips, the blocked warning when applicable, contacts as clickable `mailto:` and `tel:` chips with an opted-out marker, other open tickets, pinned notes, and a footer strip of satisfaction and last interaction.

Inline editing: click a field, it becomes an input, blur or Enter saves, Escape cancels. Show a small saving indicator and revert with a toast on failure.

On viewports below `lg`, render the panel as a collapsible section above the conversation, expanded by default on first open so the context is not hidden.

## Verification Steps

1. Open a ticket: the panel renders with no additional network request beyond the ticket fetch.
2. Open a ticket for a blocked customer: the warning and reason are prominent.
3. Open a ticket for a customer with three open tickets: all are listed and each opens correctly.
4. Pin a note on the customer profile: it appears in the panel.
5. Click an email contact: the mail client opens with the address.
6. Confirm a contact with notifications disabled is visibly marked.
7. Edit the preferred language inline: it saves, and the change appears in the audit log.
8. Sign in as a role without `customers.view`: only the display name shows, with no errors.
9. Check on a tablet: the panel collapses above the conversation and expands on tap.

## Done Criteria

- [ ] The panel data arrives with the ticket in one request and is permission-gated in the projection.
- [ ] Other open tickets, pinned notes and blocked warnings are surfaced.
- [ ] Inline editing works for the permitted fields, is validated with the same rules and is audited.
- [ ] The panel collapses sensibly on small viewports.
- [ ] Translated and RTL-correct.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
