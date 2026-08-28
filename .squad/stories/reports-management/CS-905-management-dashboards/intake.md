# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/reports-management/CS-905-management-dashboards/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 9 — Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-905-management-dashboards`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `reports, dashboards`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Management dashboards
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a manager,
I want a configurable dashboard of the numbers I care about,
So that I can see the state of the operation at a glance.

Scope:
- Dashboards composed of configurable widgets on a responsive grid.
- Widget types: stat, line, bar, pie, table, gauge and list.
- Personal, role-scoped and global dashboards.
- Drag-and-drop layout.
- Auto-refresh.
- Scheduled email delivery of reports.
- An AI usage and cost widget.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open a dashboard,
Then widgets render with current data on a responsive grid.

Given I edit a dashboard,
Then I can add, remove, resize and reposition widgets by dragging,
And the layout persists.

Given a widget,
Then I configure its metric, date range, filters and visualisation.

Given a role-scoped dashboard,
Then everyone holding that role sees it,
And only users with the manage permission can edit it.

Given auto-refresh,
Then widgets update on the configured interval without disturbing what I am doing.

Given a scheduled report,
Then it is generated and emailed on its schedule in the chosen format and language,
And a failure is recorded and visible rather than silent.

Given a widget with no data,
Then it shows an explicit empty state rather than a zero that looks like a measurement.

Given the dashboard on a phone,
Then widgets stack in a single column in their configured order.

Given the AI usage widget,
Then I see generations, acceptance rate, token usage and estimated cost.
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

- **Blocked by / related ids:** CS-901-ticket-reports
- **Depends on code areas or other stories:**

- `Dashboard`, `DashboardWidget`, `ReportDefinition` and `ScheduledReport` entities — already defined.
- CS-901 to CS-904 for the metrics widgets display.
- CS-301 for email delivery of scheduled reports.

## Extra notes (optional)

- A dashboard is only as trustworthy as its emptiest widget. Distinguishing "no data" from "zero" matters more here than anywhere else.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Widgets should request data through the same report endpoints the report pages use, so there is one implementation of each metric.

## Out of scope

- What this story explicitly does **not** cover:

- Real-time streaming dashboards.
- Custom SQL widgets.
- Public dashboard sharing.
