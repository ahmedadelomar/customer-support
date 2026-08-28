# Story 34 — Rich authoring, versioning and review lifecycle (Story: CS-602-help-articles)

## Prerequisites

- CS-601 must be complete.
- CS-104 must be complete — article images reuse that attachment store.

## Story Goal

Authors write real documentation: formatted, illustrated, versioned and reviewable. Content is
sanitised on write so every consumer — portal, agent view, chatbot — receives safe HTML.

## Context — Read These Files First

1. `.squad/stories/knowledge-base/CS-602-help-articles/intake.md`.
2. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticleVersion.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticleVersion.cs) — immutable snapshots with a change note.
3. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticle.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticle.cs) — `Version`, `ReviewDueAt`, `ReviewedById`.
4. [backend/src/CustomerSupport.Application/Files/](backend/src/CustomerSupport.Application/Files/) (CS-104) — `IAttachmentOwnerAuthorizer` already handles `KbArticle` as an owner type.

## Product rules (from story)

- **Sanitise on write, not on render.** The stored body must be safe for every consumer, including ones that do not exist yet.
- **Images go through the attachment store**, never base64 in the body — base64 bloats every query that touches the article.
- **Every publish snapshots a version.** Rollback creates a new version rather than deleting history.
- **Review-due articles are flagged, not hidden.** Stale content is better than missing content, but authors must see it.
- **In-review requires the publish permission to advance**, so an author cannot publish their own unreviewed work when the workflow requires review.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — HTML sanitisation

**File:** `backend/src/CustomerSupport.Application/KnowledgeBase/HtmlSanitizer.cs`

Use an allow-list, never a block-list — a block-list is always incomplete.

Permit: `p, br, strong, em, u, s, h2, h3, h4, ul, ol, li, blockquote, pre, code, a, img, table, thead, tbody, tr, th, td, hr, span, div`.

Attributes: `href` on `a` (http, https and mailto schemes only), `src`, `alt`, `width`, `height` on `img` (same-origin or the configured storage host only), `class` on `span` and `div` from a fixed set, `dir` and `lang` anywhere.

Strip everything else, including all `on*` handlers, `style`, `script`, `iframe`, `object`, `embed` and `form`.

Add `rel="noopener noreferrer"` and `target="_blank"` to external links.

Sanitise in the command handler before assignment, so no path into the entity bypasses it.

### 2 — Versioning and rollback

On every publish of a change, snapshot the **previous** state, then increment:

```csharp
db.KbArticleVersions.Add(new KbArticleVersion
{
    ArticleId = article.Id,
    Version = article.Version,
    Title = article.Title, Summary = article.Summary, Body = article.Body,
    ChangeNote = request.ChangeNote,
    EditedById = currentUser.UserId,
    EditedAt = clock.UtcNow,
});

article.Version += 1;
```

`RollbackArticleCommand` copies a chosen version's content onto the article and snapshots again, so the rollback itself is recorded. History is never deleted.

`GET /api/kb/articles/{id}/versions/{a}/diff/{b}` returns a line-level diff per language for the UI to render.

### 3 — Review workflow and stale-content job

`SubmitForReviewCommand` moves `Draft` to `InReview` and notifies holders of `kb.publish` in the relevant scope.

A nightly job finds articles whose `ReviewDueAt` has passed and notifies the author and reviewers once, then sets a flag so it does not notify again until the date is extended.

Expose `?reviewOverdue=true` on the article list so the authoring dashboard can surface them.

## Frontend Tasks

### 4 — Rich text editor

A bilingual editor with the two languages side by side on wide viewports and tabbed below `lg`. Toolbar: headings, bold, italic, lists, link, image, code block, quote, table.

The Arabic pane is `dir="rtl"` independently of the interface direction, so an English-speaking author can still write Arabic correctly.

Image insertion uses the shared upload component from CS-104 with `ownerType: 'KbArticle'`, inserting the returned URL. Never inline base64.

Sanitise on paste as well as on save — pasting from a word processor is how most unwanted markup arrives.

### 5 — Version history and review UI

A versions panel listing version number, editor, date and change note, with **Compare** and **Restore** actions. The diff view highlights additions and removals per language.

Restoring asks for confirmation, explaining that it creates a new version rather than erasing later ones.

Add a review-due date picker and a **Submit for review** action, with an authoring dashboard section listing overdue articles ordered by view count — the most-read stale article matters most.

## Verification Steps

1. Author an article with headings, lists, a link and an image: it renders correctly in both the agent view and the portal.
2. Paste content containing `<script>` and an `onclick` attribute: both are stripped before saving.
3. Post a body containing a script directly to the API: it is stripped server-side.
4. Confirm an inserted image is stored as an attachment and referenced by URL, not base64.
5. Publish three edits: three versions exist with their change notes.
6. Compare version 1 with version 3: the diff is accurate per language.
7. Roll back to version 1: the content reverts and a version 4 is created; versions 2 and 3 remain.
8. Set a review-due date in the past and run the job: the author is notified once, not on every run.
9. Filter the list by review-overdue: the article appears.
10. Submit a draft for review as an author without publish permission: it moves to in-review and cannot be published by them.
11. Write Arabic in the Arabic pane while the interface is English: the pane is RTL and the text is correct.

## Done Criteria

- [ ] Sanitisation is allow-list based, applied in the handler and on paste.
- [ ] Images use the shared attachment store.
- [ ] Versioning snapshots on publish; rollback creates a new version and deletes nothing.
- [ ] Diff comparison works per language.
- [ ] The review workflow and stale-content notification work, notifying once.
- [ ] The bilingual editor handles per-pane direction correctly.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
