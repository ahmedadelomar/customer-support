# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/reports-management/CS-901-ticket-reports/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 9 — Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-901-ticket-reports`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `reports`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Ticket reports
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want reports on ticket volume, resolution and backlog,
So that I can see how the operation is performing and where it is struggling.

Scope:
- A nightly aggregation job building daily rollups by branch, department, team, agent, category, priority and channel.
- Volume, resolution, backlog, reopen and escalation reports over any date range.
- Grouping and filtering by any available dimension.
- Trend comparison against a previous period.
- Export to Excel and CSV.
- Reports reading pre-aggregated data rather than scanning the ticket table.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given the nightly aggregation runs,
Then daily metrics exist for every dimension combination with activity that day,
And re-running it for the same day produces the same result rather than doubling the numbers.

Given I open a ticket report,
Then I choose a date range and grouping, and results return quickly even over a year of data.

Given a report,
Then I see created, resolved, closed, reopened and escalated counts, plus backlog at period end.

Given I compare against the previous period,
Then I see absolute and percentage change per metric.

Given I filter by branch, department, team, agent, category, priority or channel,
Then the report narrows accordingly,
And multiple filters combine.

Given averages across grouped rows,
Then they are computed correctly from stored sums and counts, not by averaging averages.

Given I export,
Then I receive an Excel or CSV file matching what is on screen, in my language.

Given today's data,
Then it is included, updated incrementally rather than only appearing after the nightly run.

Given a report over a period with no data,
Then it shows an explicit empty state rather than zeros that look like real measurements.
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

- `TicketDailyMetric` entity — already defined, with its unique dimension index and sum/count columns.
- CS-201 for tickets, CS-1203 and CS-1204 for the organisational dimensions.
- Quartz for the aggregation job.

## Extra notes (optional)

- Storing sums and counts separately rather than pre-computed averages is what allows correct recombination when rows are grouped differently.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The aggregation must be idempotent per day, because it will be re-run after backfills and after fixing data.

## Out of scope

- What this story explicitly does **not** cover:

- A custom report builder.
- A data warehouse or external BI integration.
