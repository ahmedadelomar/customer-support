# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/reports-management/CS-902-sla-performance/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 9 — Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-902-sla-performance`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `reports, sla`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
SLA performance
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want to see whether we are meeting our commitments,
So that I can act on the gap rather than discover it from a complaint.

Scope:
- First-response and resolution compliance rates.
- Breach counts and rates by dimension.
- Average and median response and resolution times, in working hours.
- Breach analysis: which categories, priorities, agents and times of day.
- Trends over time.
- Per-policy performance.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given the SLA report,
Then I see first-response and resolution compliance as percentages,
And the counts they derive from, so the percentage can be checked.

Given breach analysis,
Then I see breaches broken down by category, priority, department, agent and channel,
And I can drill into the actual tickets behind any figure.

Given response and resolution times,
Then they are reported in working hours consistent with the SLA calculation,
Not in wall-clock time, which would look better and mean nothing.

Given both average and median,
Then both are shown, because a few extreme tickets distort the average.

Given a trend view,
Then compliance over time is shown, with a target line.

Given tickets with no SLA policy,
Then they are excluded from compliance rates and reported separately,
So they cannot silently inflate or deflate the figures.

Given a paused ticket,
Then paused time is excluded from elapsed time, exactly as the SLA engine excludes it.

Given per-policy performance,
Then I can see which policies are routinely missed, which usually means the target is unrealistic.
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

- CS-501 for the SLA clocks and working-hours calculation.
- CS-901 for the aggregation infrastructure.
- `TicketDailyMetric` SLA columns — already defined.

## Extra notes (optional)

- Reporting wall-clock times when the SLA is measured in working hours would produce numbers that contradict the breach flags. The two must use the same calculation.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Median cannot be aggregated from daily rollups. Compute it from the ticket table over the selected range, with an index to support it.

## Out of scope

- What this story explicitly does **not** cover:

- Predicting future breaches.
- Automatic SLA target tuning.
