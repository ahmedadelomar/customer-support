# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ai-features/CS-704-suggested-solutions/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 7 — AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-704-suggested-solutions`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `ai`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Suggested solutions
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want the knowledge base searched semantically for solutions to the ticket in front of me,
So that I find the right article even when it uses different words than the customer did.

Scope:
- Semantic retrieval over knowledge base content, beyond keyword matching.
- Suggestions ranked by relevance and by proven effectiveness.
- Presentation alongside the existing rule-based suggestions, clearly labelled.
- Insertion into a reply, tracked like any other solution use.
- Identification of tickets where no good solution exists, feeding the content-gap report.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given an open ticket,
Then relevant solutions are suggested based on the meaning of the conversation, not only keyword overlap.

Given a suggestion,
Then it shows why it was suggested and a relevance indicator,
And it is labelled as AI-sourced, distinct from the category-based suggestions.

Given both AI and rule-based suggestions exist,
Then they appear in the same panel, each labelled by source,
Because the agent should see one ranked list, not two competing panels.

Given I insert a suggested solution,
Then it is recorded as a use and credited on resolution, exactly as a rule-based one is.

Given no article scores above the relevance floor,
Then nothing is suggested,
And the ticket is recorded as a content gap.

Given the content-gap report,
Then AI-identified gaps appear alongside zero-result searches.

Given retrieval fails,
Then the rule-based suggestions still show, because the panel must not depend on the model.

Given a ticket in Arabic,
Then Arabic articles are retrieved and ranked correctly.
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

- **Blocked by / related ids:** CS-703-automatic-categorization
- **Depends on code areas or other stories:**

- CS-603 for the suggestion panel and the effectiveness model this extends.
- CS-604 for the existing search infrastructure.
- CS-701 for the AI service.
- `AiSuggestion` with `SuggestedArticleId` — already defined.

## Extra notes (optional)

- CS-603 deliberately defined the panel DTO with a `Source` field so this story adds entries rather than replacing the screen.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Semantic retrieval needs embeddings and a vector store, or a hybrid of keyword search plus model reranking. The hybrid needs no new infrastructure and is the pragmatic first implementation.

## Out of scope

- What this story explicitly does **not** cover:

- Generating new articles from resolved tickets.
- Replacing the rule-based panel.
