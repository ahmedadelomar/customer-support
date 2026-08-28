# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-management/CS-201-create-track-tickets/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 2 — Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-201-create-track-tickets`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `tickets, core`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Create and track tickets
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want to raise a ticket for a customer and follow it through to resolution,
So that no request is lost and everyone can see where it stands.

Scope:
- Ticket creation from the agent workspace against an existing or newly created customer.
- A public reference number the customer can quote.
- The ticket detail screen: conversation thread, customer panel, properties sidebar.
- Replying to the customer and adding internal notes on the same thread.
- The ticket list with the filters an agent actually uses: my tickets, my team, unassigned, by status, priority, category, channel and date.
- Saved views for the filter combinations an agent returns to daily.
- Merging a duplicate ticket into another.

This is the central aggregate of the product. Every other feature reads or writes it.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I create a ticket,
Then a unique number in the form TCK-2026-000123 is generated,
And the ticket starts in the default status with the default priority unless I set otherwise,
And its department is resolved from the category, then the channel, then the system default.

Given I create a ticket for a blocked customer,
Then the request is refused with an explanation.

Given a ticket is created,
Then a Created event is appended to its history,
And an interaction timeline entry is written for the customer.

Given I open a ticket,
Then I see the conversation newest-last, the customer panel with profile and open ticket count, and the properties sidebar with status, priority, category, assignee, department and SLA due times.

Given I reply to the customer,
Then the reply is added to the thread as an outbound message,
And the ticket status moves out of New,
And LastAgentReplyAt is set.

Given I add an internal note,
Then it is visibly distinct in the thread,
And it is never sent to the customer nor shown in the portal.

Given I change any ticket property,
Then a corresponding event is appended to the history with the old and new display values.

Given the ticket list,
Then I can filter by assignment, status, priority, category, channel, department and date range,
And filter state is reflected in the URL,
And I can save a filter combination as a named view.

Given I merge ticket B into ticket A,
Then B's messages are moved to A,
And B is marked merged and becomes read-only,
And both histories record the merge.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| — | None. |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CS-101-customer-profiles
- **Depends on code areas or other stories:**

- CS-1001 for permissions, CS-101 for customers, CS-1203 for departments.
- `Ticket`, `TicketMessage`, `TicketEvent` entities — already defined.
- `ITicketEventRecorder` and `TicketEventRecorder` — already implemented.
- `IReferenceNumberGenerator` — already implemented, but requires the `TicketNumbers` sequence created in CS-1001.
- CS-103 for the interaction recorder.
- CS-104 for attachments on messages.

## Extra notes (optional)

- Ticket numbers are quoted to customers and must never be reused or reassigned.
- Merging is common in email support, where a customer replies to an old thread and creates a second ticket.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The `Ticket` entity already carries denormalised SLA columns mirroring `TicketSlaClock`. They exist so list filtering avoids a join; CS-501 owns keeping them in sync.

## Out of scope

- What this story explicitly does **not** cover:

- SLA calculation — CS-501.
- Automatic assignment — CS-502.
- Inbound channel ingestion — Section 3.
- AI summaries and suggested replies — Section 7.
