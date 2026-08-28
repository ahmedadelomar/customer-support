# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/agent-dashboard/CS-403-tasks-reminders/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 4 — Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-403-tasks-reminders`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `workspace`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Tasks and reminders
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want to set myself tasks and timed reminders,
So that follow-up work does not depend on my memory.

Scope:
- Tasks owned by an agent, optionally attached to a ticket or customer.
- Due dates, priority and status.
- Reminders that fire at a set time through the agent's chosen channels.
- Snoozing and dismissing.
- A task list on the dashboard and a task panel on the ticket screen.
- Assigning a task to a colleague.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I create a task,
Then it has a title, optional description, due date, priority and owner,
And it can be linked to a ticket or a customer.

Given a task with a reminder,
Then the reminder fires at the set time through the channels I chose,
And it fires once, not repeatedly.

Given a reminder fires,
Then I can snooze it for a chosen interval or dismiss it.

Given I complete a task,
Then the completion time and the completing user are recorded.

Given the dashboard,
Then my open tasks are listed by due date,
And overdue tasks are visually distinct.

Given a ticket,
Then its linked tasks appear on the ticket screen,
And I can add one without leaving it.

Given I assign a task to a colleague,
Then they are notified and it appears in their list.

Given a ticket is closed with open linked tasks,
Then I am warned before closing rather than silently orphaning them.

Given a reminder set in the past,
Then the request is rejected with a clear message.
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

- `AgentTask` and `Reminder` entities — already defined, including the `(IsSent, RemindAt)` dispatch index.
- CS-504 for notification delivery.
- CS-201 for the ticket link.

## Extra notes (optional)

- The reminder dispatch job must be idempotent: a job that runs twice must not send two reminders.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- `Reminder.Channels` is a comma-separated `NotificationChannel` list, resolved against the user preferences at send time.

## Out of scope

- What this story explicitly does **not** cover:

- Recurring tasks.
- Calendar integration.
- Project-style task boards.
