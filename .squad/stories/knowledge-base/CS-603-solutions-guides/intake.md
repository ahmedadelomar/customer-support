# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/knowledge-base/CS-603-solutions-guides/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 6 — Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-603-solutions-guides`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `knowledge-base, content`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Solutions and guides
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want articles linked to the problems they solve,
So that the right solution surfaces on the right ticket instead of being searched for.

Scope:
- Solution articles associated with ticket categories.
- Step-by-step guides with ordered, checkable steps.
- Suggested solutions surfaced on the ticket screen by category.
- One-click insertion of a solution into a reply.
- Effectiveness tracking: which solutions actually resolve tickets.
- Identifying categories with no solution coverage.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a solution article,
Then I can associate it with one or more ticket categories.

Given an agent opens a ticket,
Then solutions associated with its category are suggested in a panel,
Ordered by effectiveness rather than by recency.

Given a suggested solution,
Then the agent can preview it and insert it into the reply in one action,
And the article's used-in-reply count increases.

Given a ticket is resolved after a solution was inserted,
Then that is recorded as a successful use,
So effectiveness reflects outcomes rather than clicks.

Given a guide,
Then it has ordered steps, each with a title and body,
And customers can tick steps off as they follow them, with progress kept locally in their browser.

Given the coverage report,
Then I see which ticket categories have no associated solution,
Ranked by ticket volume so the biggest gaps come first.

Given a solution is inserted in the ticket's language,
Then the correct language body is used regardless of the agent's interface language.

Given a category with several solutions,
Then the most effective is suggested first.
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

- **Blocked by / related ids:** CS-602-help-articles
- **Depends on code areas or other stories:**

- CS-601 and CS-602 for the article model and authoring.
- `KbArticle.RelatedCategoryIds` and `UsedInReplyCount` — already defined.
- CS-201 for the ticket screen the suggestion panel sits on.
- CS-704, which will layer AI-suggested solutions on top of this rule-based panel.

## Extra notes (optional)

- Measuring effectiveness by resolution outcome rather than by insertion count is what stops a badly written but frequently inserted article ranking first forever.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- This story is the rule-based baseline that CS-704 improves on. Keep the panel contract stable so the AI version can slot in beside it.

## Out of scope

- What this story explicitly does **not** cover:

- AI-suggested solutions — CS-704.
- Decision-tree troubleshooters.
