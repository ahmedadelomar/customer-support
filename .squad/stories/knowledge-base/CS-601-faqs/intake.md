# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/knowledge-base/CS-601-faqs/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 6 — Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-601-faqs`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `knowledge-base, content`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
FAQs
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want answers to common questions without contacting support,
So that I get help immediately and support handles fewer repeat questions.

Scope:
- FAQ content authored in both languages, organised by category.
- A public FAQ page on the portal, grouped and expandable.
- Helpful and not-helpful voting with an optional comment.
- View counting.
- Featured FAQs pinned to the top.
- Agents inserting an FAQ link into a ticket reply.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an author,
When I create an FAQ,
Then I set a title, summary and body in both languages, and a category,
And it starts as a draft.

Given a draft,
Then it is not visible in the portal until published.

Given a published public FAQ,
Then customers see it on the portal grouped by category,
And featured items appear first.

Given I view an FAQ,
Then its view count increases,
But repeated views within one session do not inflate the count.

Given I vote helpful or not helpful,
Then the count updates,
And I cannot vote twice on the same article from the same session.

Given I vote not helpful,
Then I am invited to say why, optionally.

Given an FAQ has an empty body in one language,
Then the other language is shown rather than a blank page.

Given an agent replying to a ticket,
Then they can search FAQs and insert a link,
And the article's used-in-reply count increases.

Given an FAQ is archived,
Then it disappears from the portal but its URL still resolves for anyone holding an old link, showing an archived notice.
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

- `KbCategory`, `KbArticle`, `KbArticleFeedback` entities — already defined, with `KbArticleType.Faq`.
- CS-1001 for the `kb.*` permissions.
- CS-804 for the portal surface that renders FAQs.

## Extra notes (optional)

- FAQs, help articles, solutions and guides share one entity distinguished by `Type`, because they share authoring, search, feedback and analytics. Splitting them into separate tables would duplicate all four.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- `UsedInReplyCount` measures real usefulness better than view count: it counts times an agent judged the article good enough to send.

## Out of scope

- What this story explicitly does **not** cover:

- AI-generated FAQ content — CS-704.
- Article comments and discussion threads.
