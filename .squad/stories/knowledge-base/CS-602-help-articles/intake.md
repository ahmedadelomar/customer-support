# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/knowledge-base/CS-602-help-articles/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 6 — Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-602-help-articles`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `knowledge-base, content`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Help articles
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a knowledge author,
I want to write and maintain longer help articles with formatting and images,
So that customers and agents have proper documentation, not just short answers.

Scope:
- Rich-text authoring with a sanitised HTML body.
- Images and file attachments inside articles.
- A draft, in-review, published, archived lifecycle.
- Version history with diff and rollback.
- Review-due dates flagging stale content.
- Internal articles for agent-only runbooks.
- Related-article links.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I author an article,
Then I can format text, add links, lists, headings, images and code blocks,
And the stored HTML is sanitised so no script can be persisted.

Given I upload an image inside an article,
Then it is stored through the attachment store and referenced by URL, not embedded as base64.

Given I save changes to a published article,
Then a new version snapshot is created with my change note,
And the version number increases.

Given version history,
Then I can compare any two versions and roll back to an earlier one,
And rolling back creates a new version rather than deleting history.

Given I set a review-due date,
Then the article is flagged in the authoring dashboard when that date passes.

Given an article marked internal,
Then it never appears in the portal and is visible only to agents.

Given I submit an article for review,
Then it moves to in-review and reviewers are notified,
And publishing requires the publish permission.

Given a published article,
Then it has a stable URL slug that does not change when the title is edited.

Given related articles are linked,
Then they are shown at the foot of the article.

Given an article body in one language only,
Then the portal shows the available language with a notice rather than an empty page.
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

- **Blocked by / related ids:** CS-601-faqs
- **Depends on code areas or other stories:**

- CS-601 for the shared article model and category tree.
- `KbArticleVersion` entity — already defined.
- CS-104 for the attachment store images reuse.

## Extra notes (optional)

- Sanitising on write, not on render, means the stored content is safe for every consumer, including future ones such as the chatbot.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The slug is stable and separate from the title precisely so links survive editorial changes.

## Out of scope

- What this story explicitly does **not** cover:

- Collaborative simultaneous editing.
- Translation memory or machine translation between the two languages.
