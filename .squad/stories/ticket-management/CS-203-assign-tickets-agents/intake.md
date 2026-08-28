# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-management/CS-203-assign-tickets-agents/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 2 — Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-203-assign-tickets-agents`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `tickets`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Assign tickets to agents
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a team leader or agent,
I want to assign tickets to a team or a specific agent, and claim unassigned work,
So that every ticket has a clear owner and nothing sits in a queue unnoticed.

Scope:
- Manual assignment to a team or an individual, and reassignment.
- Self-assignment from the unassigned queue.
- Bulk assignment from the ticket list.
- Availability and capacity awareness: warn before assigning to an away or at-capacity agent.
- Notification to the new assignee.
- Every assignment change recorded in the ticket history.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a ticket,
When I assign it to an agent,
Then AssignedAgentId and AssignedAt are set,
And an Assigned event is appended with the agent's display name,
And the agent receives a notification.

Given I reassign a ticket,
Then the history records both the previous and the new assignee.

Given I unassign a ticket,
Then it returns to the team queue and an Unassigned event is recorded.

Given an unassigned ticket in my team's queue,
When I claim it,
Then it is assigned to me,
And if another agent claimed it moments earlier, I receive a clear conflict message rather than silently overwriting them.

Given I assign to an agent whose availability is Away or who is at their concurrent-ticket cap,
Then I am warned and must confirm, because there are legitimate reasons to override.

Given I select several tickets in the list,
When I bulk assign them,
Then each is assigned, each gets its own history event and notification,
And a partial failure reports which tickets failed and why rather than failing the whole batch.

Given I lack tickets.assign,
Then assignment controls are not shown, and calling the endpoint returns 403.

Given assignment to a team without an agent,
Then the ticket sits in that team's queue with no individual owner.
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

- CS-201 for the ticket, CS-1203 for teams and capacity.
- `TeamMember.MaxConcurrentTickets` and `ApplicationUser.AvailabilityStatus` — already defined.
- CS-504 for notification delivery.

## Extra notes (optional)

- Claiming is inherently racy: two agents watching the same queue will click at the same moment. Handle it with a conditional update rather than read-then-write.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Manual assignment must respect the same capacity rules automatic assignment (CS-502) uses, or the two will disagree and confuse agents.

## Out of scope

- What this story explicitly does **not** cover:

- Rule-driven automatic assignment — CS-502.
- Skill-based routing configuration — the entity exists, the editor is in CS-502.
