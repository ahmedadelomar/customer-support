# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/agent-dashboard/CS-405-team-collaboration/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 4 — Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-405-team-collaboration`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `workspace, collaboration`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Team collaboration
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want to bring colleagues into a ticket without sending anything to the customer,
So that I can get help while keeping the customer conversation clean.

Scope:
- At-mentions of colleagues in internal notes.
- Watchers who follow a ticket without owning it.
- A collaboration inbox of mentions directed at me.
- Presence indicators showing who else is viewing a ticket.
- Handover notes when reassigning.
- A collision warning when two agents compose a reply at once.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I type @ in an internal note,
Then a picker suggests colleagues,
And selecting one inserts a mention.

Given I post a note with mentions,
Then each mentioned colleague is notified and added as a watcher,
And the mention appears in their collaboration inbox.

Given a mention in my inbox,
Then I can open the ticket directly and mark the mention read.

Given I watch a ticket,
Then I am notified of activity on it without being the assignee,
And I can stop watching at any time.

Given another agent is viewing the same ticket,
Then I see a presence indicator naming them.

Given another agent starts composing a reply while I am composing one,
Then I see a collision warning before I send,
Because two replies to the same customer minutes apart looks disorganised.

Given I reassign a ticket,
Then I can add a handover note recorded as an internal note and shown to the new assignee.

Given a mention of someone without access to the ticket's department,
Then I am warned that they cannot see it.

Given mentions, they never appear in any customer-facing view.
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

- `TicketMention` and `TicketWatcher` entities — already defined.
- CS-201 for internal notes, CS-203 for reassignment, CS-303 for the SignalR infrastructure presence reuses.
- CS-504 for notifications.

## Extra notes (optional)

- Presence and collision warnings reuse the SignalR hub from CS-303 rather than adding a second real-time mechanism.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Mentions are parsed and stored as structured `TicketMention` rows, not left as text to be re-parsed on render.

## Out of scope

- What this story explicitly does **not** cover:

- Real-time collaborative editing of a reply.
- A separate team chat product.
