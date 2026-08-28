# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-management/CS-103-interaction-history/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 1 — Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-103-interaction-history`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `customers`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Interaction history
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want one chronological timeline of everything that has passed between us and a customer,
So that I can pick up a conversation without reading five separate tickets.

Scope:
- A unified timeline across channels: emails, WhatsApp, SMS, live chats, web forms, portal activity and satisfaction responses.
- Written by the channel ingestion pipeline and the ticket workflow as a read-optimised projection, never edited by hand.
- Filterable by channel, direction and date range.
- Each entry links back to its source ticket or message.
- Infinite scroll, because an established customer accumulates thousands of entries.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a customer profile,
When I open the Interaction history tab,
Then I see a reverse-chronological timeline of every interaction across all channels.

Given a timeline entry,
Then it shows the channel, direction, timestamp, subject or preview, and the agent involved,
And clicking it opens the source ticket or message.

Given I filter by channel or direction or a date range,
Then only matching entries are shown.

Given a customer with thousands of interactions,
Then entries load in pages as I scroll rather than all at once.

Given an inbound message arrives on any channel,
Then an Interaction row is written as part of the same transaction that creates the ticket message,
And the customer's LastInteractionAt is updated.

Given an agent replies,
Then an outbound Interaction row is written.

Given a customer submits a satisfaction survey,
Then that also appears on the timeline.

Given the timeline, no API allows editing or deleting an entry: it is a projection of what actually happened.
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

- CS-101 for the profile shell.
- `Interaction` entity with its `(CustomerId, OccurredAt)` index — already defined.
- CS-201 and Section 3 stories, which are the sources that write timeline entries.

## Extra notes (optional)

- Interaction is a projection. Writing it inside the same transaction as its source is what keeps it honest; a separate job would drift.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Use keyset pagination on `(OccurredAt, Id)` rather than offset paging. Offset paging over a long timeline gets slower the further back you scroll and can skip rows when new ones arrive.

## Out of scope

- What this story explicitly does **not** cover:

- Editing or annotating timeline entries — notes are CS-104.
- Exporting the timeline.
- Cross-customer activity feeds.
