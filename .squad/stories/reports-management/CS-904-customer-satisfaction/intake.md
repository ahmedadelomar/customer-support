# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/reports-management/CS-904-customer-satisfaction/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 9 — Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-904-customer-satisfaction`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `reports, satisfaction`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Customer satisfaction
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want to see satisfaction trends and read what customers actually said,
So that I can act on the reasons rather than only the score.

Scope:
- CSAT score, response rate and distribution.
- Trends over time and by dimension.
- Comment analysis, with low-score comments surfaced first.
- Follow-up tracking for low scores.
- Per-agent and per-category satisfaction.
- Response rate reporting, since a high score from few responses means little.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given the satisfaction report,
Then I see the average score, the response rate, and the distribution across the scale.

Given the response rate,
Then it is shown alongside every score,
Because an average from 3 responses is not comparable to one from 300.

Given trends,
Then I see satisfaction over time and can compare periods.

Given breakdowns,
Then I can see satisfaction by agent, category, channel, department and branch.

Given comments,
Then I can read them, filtered by score, with low scores surfaced first,
And each links to its ticket.

Given a low score,
Then I see whether a follow-up ticket was created and its current status,
So low scores can be confirmed as handled rather than assumed.

Given an agent with very few responses,
Then their score is marked as low-confidence.

Given the report, it uses the agent credited at survey send time,
So a later reassignment does not move a score onto someone who was not involved.
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

- **Blocked by / related ids:** CS-805-submit-feedback
- **Depends on code areas or other stories:**

- CS-805 for surveys and responses.
- CS-901 for the aggregation infrastructure.
- `CsatSurvey` and the CSAT sum and count columns on `TicketDailyMetric` — already defined.

## Extra notes (optional)

- Response rate is as important as the score. Reporting one without the other invites wrong conclusions.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Storing the CSAT sum and count separately on the rollup is what makes correct averages possible across any grouping.

## Out of scope

- What this story explicitly does **not** cover:

- Sentiment analysis of comments — an AI feature extension.
- Benchmarking against other organisations.
