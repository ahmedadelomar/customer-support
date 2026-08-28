# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-management/CS-205-ticket-history/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 2 — Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-205-ticket-history`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `tickets, audit`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Ticket history
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent or manager,
I want a complete, readable record of everything that happened to a ticket,
So that I can understand how it got here and answer questions about how it was handled.

Scope:
- An append-only event log covering every mutation, written in the same transaction as the change.
- Human-readable rendering: "Sara changed status from Open to Pending Customer".
- Display values captured at write time so history survives later renames.
- A merged view of events and messages on one timeline.
- Filtering by event type, and a system-events toggle.
- Clear attribution for automation, naming the rule that acted.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given any ticket mutation,
Then a TicketEvent is appended in the same transaction,
So the history can never disagree with the ticket state.

Given an event,
Then it records the type, actor, timestamp, field, old and new values, and old and new display values.

Given a lookup is renamed after an event was recorded,
Then the history still shows the name as it was at the time.

Given an automation acted,
Then the event is marked system-generated and names the rule that triggered it.

Given I open the History tab,
Then events and messages appear on one timeline in chronological order,
And each entry renders as a readable sentence in the active language.

Given I filter by event type,
Then only matching entries show.

Given the system-events toggle is off,
Then routine automation noise is hidden and only human actions remain.

Given a long-running ticket,
Then history pages as I scroll rather than loading every event at once.

Given any user,
Then no API allows editing or deleting a history entry.
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

- **Blocked by / related ids:** CS-201-create-track-tickets
- **Depends on code areas or other stories:**

- CS-201 for the ticket and the detail screen.
- `TicketEvent` entity and `ITicketEventRecorder` — already implemented.
- Every other ticket story, since each must record its own events.

## Extra notes (optional)

- Capturing display values at write time is the detail that makes history trustworthy years later.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Render sentences from translation keys parameterised by the event, not by concatenating strings — concatenation cannot be translated into Arabic correctly.

## Out of scope

- What this story explicitly does **not** cover:

- Exporting a ticket history as PDF.
- Cross-ticket activity reports — Section 9.
