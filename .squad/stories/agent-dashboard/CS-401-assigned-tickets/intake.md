# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/agent-dashboard/CS-401-assigned-tickets/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 4 — Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-401-assigned-tickets`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `workspace`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Assigned tickets
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want a home screen that tells me what to work on next,
So that I start my shift knowing my priorities instead of hunting through a list.

Scope:
- KPI tiles: assigned to me, due today, approaching SLA breach, breached, resolved this week.
- A "next up" queue ordered by urgency rather than by creation date.
- Quick filters for my open tickets, awaiting my reply, and unassigned in my team.
- One-click actions from the dashboard: open, claim, reply.
- Auto-refresh so the numbers stay current without a manual reload.
- Personal targets compared against team averages.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open the dashboard,
Then I see tiles for assigned to me, due today, approaching breach, breached, and resolved this week,
And each tile links to the ticket list with the matching filter applied.

Given the next-up queue,
Then tickets are ordered by SLA urgency first, then priority, then age,
So the most at-risk work is at the top rather than the oldest.

Given a ticket in the queue,
Then I can open it, or claim it if unassigned, without leaving the dashboard.

Given the dashboard is open,
Then the tiles refresh on the configured interval,
And refreshing does not lose my scroll position or any open menu.

Given I have no assigned tickets,
Then an empty state offers to show my team's unassigned queue instead of showing an empty screen.

Given my personal statistics,
Then I see my resolved count and average first-response time this week alongside the team average,
Presented as context rather than as a ranking.

Given the dashboard on a phone,
Then tiles stack and the queue renders as cards.

Given I lack tickets.view,
Then the dashboard shows only the sections I am permitted to see.
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

- CS-201 for tickets and the statistics endpoint.
- CS-501 for SLA due times and breach flags.
- CS-203 for claiming.
- `DashboardPage` placeholder — already scaffolded in the skeleton.

## Extra notes (optional)

- Ordering by SLA urgency rather than creation date is the difference between a dashboard that helps and a list that just exists.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The `Ticket` entity carries denormalised SLA columns precisely so this query needs no join.

## Out of scope

- What this story explicitly does **not** cover:

- Configurable dashboard widgets — CS-905.
- Team-wide management dashboards — Section 9.
