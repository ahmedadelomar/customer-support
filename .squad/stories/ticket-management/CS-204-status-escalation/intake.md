# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-management/CS-204-status-escalation/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 2 — Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-204-status-escalation`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `tickets, workflow`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Status and escalation
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent and manager,
I want a configurable status workflow and the ability to escalate,
So that a ticket's state is always meaningful and stuck work gets attention.

Scope:
- Configurable statuses, each mapped to a fixed kind so behaviour stays predictable when they are renamed.
- Status transition rules and required fields, such as a resolution note before resolving.
- Pending statuses that pause the SLA resolution clock.
- Reopening a resolved ticket, with a counter.
- Manual escalation with a level and a reason, notifying the manager.
- Auto-close of resolved tickets after a configurable period.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given the status list,
Then each status has a code, bilingual name, kind, colour, order, and the terminal, pauses-SLA, portal-visible and active flags.

Given I change a ticket's status,
Then a StatusChanged event records the old and new display names,
And the change is reflected immediately in the list.

Given I move a ticket to a status whose kind is Resolved,
Then a resolution note is required,
And ResolvedAt and ResolvedById are set,
And a satisfaction survey is queued.

Given I move a ticket to a status whose kind pauses SLA,
Then the resolution clock pauses,
And it resumes when the ticket returns to an active status.

Given a customer replies to a resolved ticket,
Then the ticket reopens automatically,
And ReopenCount increases,
And the SLA clock resumes rather than restarting.

Given a resolved ticket with no further activity for the configured period,
Then it is closed automatically,
And a Closed event marks it as system-generated.

Given I escalate a ticket manually,
Then EscalationLevel increases, EscalatedAt is set, a reason is required,
And the department manager and any watchers are notified.

Given a ticket in a terminal status,
Then it is read-only except for reopening.

Given a status still used by open tickets,
Then it cannot be deleted, only deactivated.
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

- CS-201 for the ticket, CS-202 for the lookup admin pattern.
- `TicketStatus` with `Kind`, `IsTerminal` and `PausesSla` — already defined.
- CS-501 for the SLA clock this story pauses and resumes.
- CS-805 for the satisfaction survey queued on resolution.

## Extra notes (optional)

- Mapping every status to a fixed `TicketStatusKind` is what lets administrators rename statuses freely without breaking SLA, reporting or the portal.
- Reopening must resume rather than restart the SLA clock, or a reopened breach silently disappears from reports.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- `tickets.autoCloseResolvedAfterDays` is a seeded system setting from CS-1004.

## Out of scope

- What this story explicitly does **not** cover:

- Rule-driven automatic escalation — CS-503.
- A visual workflow designer.
