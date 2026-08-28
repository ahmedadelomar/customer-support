# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/platform/CS-1203-multi-department/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 12 — Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1203-multi-department`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `platform, foundation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Multi-department
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want tickets to belong to departments and teams, and agents to work within theirs,
So that Billing does not see Technical Support's queue and routing has something to route to.

Scope:
- Department and team CRUD, with team membership.
- A department on every ticket, defaulted by category or channel and changeable by an agent.
- Queue visibility scoped by department unless the user holds the see-all permission.
- Transferring a ticket to another department, recorded in ticket history.
- Per-member concurrent-ticket caps and a rotation order, which automatic assignment depends on.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an administrator,
When I open Administration > Departments,
Then I can create, edit and deactivate departments and the teams inside them.

Given a team,
Then I can add and remove members, set a lead, set each member's maximum concurrent tickets, and set the rotation order.

Given a ticket is created,
Then its department is taken from the category default, then the channel account default, then the system default, in that order.

Given I am an agent without tickets.view.all,
When I open the ticket list,
Then I see only tickets in my department or assigned to me.

Given I hold tickets.view.all,
Then I see every ticket within my accessible branches.

Given I transfer a ticket to another department,
Then the assignee is cleared,
And a DepartmentChanged event is appended to the ticket history with the old and new names,
And the SLA clock continues rather than restarting.

Given a department still has open tickets or active teams,
When I try to deactivate it,
Then the request is refused with an explanation.

Given department and team names, they are stored and displayed in both Arabic and English.
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

- **Blocked by / related ids:** CS-1001-users-roles
- **Depends on code areas or other stories:**

- `Department`, `Team` and `TeamMember` entities — already defined.
- `DbSeeder` already seeds four departments (GEN, TECH, BILL, SALES).
- CS-1001 for users, since team membership references them.
- CS-201 for the ticket entity the department is assigned on.

## Extra notes (optional)

- Transferring must not restart the SLA clock. Customers do not care about internal routing, and restarting hides breaches.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- `ApplicationUser.DepartmentId` and `TeamMember.MaxConcurrentTickets` already exist.

## Out of scope

- What this story explicitly does **not** cover:

- Department-level SLA policies — the column exists, the editor is in CS-501.
- Cross-department approval workflows.
