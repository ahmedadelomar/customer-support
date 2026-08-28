# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ai-features/CS-703-automatic-categorization/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 7 — AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-703-automatic-categorization`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `ai`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Automatic categorization
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want incoming tickets classified automatically,
So that routing works even when customers pick the wrong category or none at all.

Scope:
- Category and priority suggested at ticket creation.
- A confidence score, with automatic application only above a configurable threshold.
- Agent override, always available and always recorded.
- Accuracy measurement against what agents actually chose.
- Batch reclassification of historical tickets.
- Sentiment and urgency detection to inform priority.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a ticket is created through any channel,
Then a category and priority are suggested from the subject and description,
And a confidence score is recorded.

Given confidence is above the automatic-apply threshold,
Then the category is applied,
And a ticket event records that automation set it, naming the confidence.

Given confidence is below that threshold,
Then the suggestion is shown to the agent but not applied.

Given an agent changes the category,
Then their choice is recorded against the suggestion,
So accuracy can be measured against real decisions rather than assumed.

Given the accuracy report,
Then I see agreement rate overall and per category,
And I can identify categories the model gets wrong.

Given detected sentiment is strongly negative,
Then a higher priority is suggested with the reason given.

Given classification fails or times out,
Then the ticket is created with the default category and the failure is logged.

Given the category tree changes,
Then classification uses the current active, portal-relevant categories rather than a stale list.

Given batch reclassification,
Then it runs in the background with progress reporting and can be cancelled.
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

- **Blocked by / related ids:** CS-701-ticket-summaries
- **Depends on code areas or other stories:**

- CS-701 for the AI service.
- CS-201 for ticket creation, CS-202 for the category tree and priorities.
- `Ticket.AiCategoryConfidence` — already defined.
- CS-502, whose routing rules act on the resulting category.

## Extra notes (optional)

- Automatic application above a threshold is what makes this useful; without it, an agent still has to confirm every ticket and nothing is saved.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Use structured output so the model returns a category id and confidence rather than prose that has to be parsed.

## Out of scope

- What this story explicitly does **not** cover:

- Creating new categories automatically.
- Automatic assignment, which is CS-502 and consumes this output.
