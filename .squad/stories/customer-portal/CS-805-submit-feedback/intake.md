# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-portal/CS-805-submit-feedback/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 8 — Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-805-submit-feedback`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `portal, satisfaction`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Submit feedback
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to rate the support I received,
So that the organisation knows whether it is actually helping.

Scope:
- A satisfaction survey issued when a ticket is resolved.
- A single-use tokenised link so responding needs no sign-in.
- A rating, an optional recommendation score and a free-text comment.
- Reminders for unanswered surveys.
- Automatic follow-up on a low score.
- General feedback outside any ticket.
- Feeding scores into the customer profile and the reports.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a ticket is resolved,
Then a survey is created with a single-use token and an expiry,
And it is sent through the customer's preferred channel in their language.

Given I open the survey link,
Then I can respond without signing in,
And the token works only once.

Given the token is expired or already used,
Then I see a clear message rather than an error.

Given I respond,
Then the score, optional recommendation score and comment are recorded with a timestamp,
And the agent credited is the one recorded when the survey was sent, not the current assignee.

Given a low score,
Then a follow-up ticket is created automatically and linked to the original,
And the support manager is notified.

Given I do not respond,
Then up to a configured number of reminders are sent, then no more.

Given a ticket is reopened after a survey was sent,
Then the outstanding survey is cancelled, because rating an unresolved ticket is meaningless.

Given my responses,
Then my customer profile satisfaction score is recalculated,
And the scores feed the satisfaction report.

Given I want to leave feedback outside a ticket,
Then the portal offers a general feedback form.
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

- **Blocked by / related ids:** CS-802-track-requests
- **Depends on code areas or other stories:**

- `CsatSurvey` entity — already defined, with a unique token and one survey per ticket.
- CS-204 for the resolution event that queues the survey.
- CS-301 and CS-304 for delivery, CS-504 for the manager notification.
- CS-904 for the satisfaction report these scores feed.

## Extra notes (optional)

- Crediting the agent recorded at send time, not the current assignee, prevents a later reassignment from silently moving a good or bad score to someone else.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The one-survey-per-ticket unique index means a resolve-reopen-resolve cycle must update the existing survey rather than inserting a second.

## Out of scope

- What this story explicitly does **not** cover:

- Configurable survey question sets.
- Net Promoter Score campaigns outside support.
