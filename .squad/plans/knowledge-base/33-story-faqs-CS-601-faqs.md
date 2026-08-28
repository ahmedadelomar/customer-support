# Story 33 — Knowledge base foundation and FAQs (Story: CS-601-faqs)

## Prerequisites

- CS-1001 must be complete for the `kb.*` permissions.
- This story creates the category tree and article lifecycle the rest of the feature builds on.

## Story Goal

The knowledge base exists: a category tree, articles with a draft-to-published lifecycle, bilingual
content with sensible fallback, and voting that tells authors what is working.

## Context — Read These Files First

1. `.squad/stories/knowledge-base/CS-601-faqs/intake.md`.
2. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticle.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticle.cs) — read the class remark on why one entity serves all four content types. Note `UsedInReplyCount`.
3. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbCategory.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbCategory.cs) — same materialised `Path` pattern as `TicketCategory`.
4. [backend/src/CustomerSupport.Domain/Enums/ContentEnums.cs](backend/src/CustomerSupport.Domain/Enums/ContentEnums.cs) — `KbArticleType` and `PublicationStatus`.
5. [backend/src/CustomerSupport.Application/Tickets/Categories/](backend/src/CustomerSupport.Application/Tickets/Categories/) (CS-202) — the tree maintenance code to mirror for KB categories.
6. [backend/src/CustomerSupport.Domain/Common/LocalizedText.cs](backend/src/CustomerSupport.Domain/Common/LocalizedText.cs) — `For(culture)` already implements the fallback rule.

## Product rules (from story)

- **One entity, four types.** `KbArticleType` distinguishes them; the lifecycle, search and feedback are shared.
- **Only `Published` and `IsPublic` articles reach the portal.** Draft, in-review, archived and internal never do.
- **Language fallback, never a blank page.** An empty Arabic body shows the English one with a notice.
- **View counting is de-duplicated per session**, or the metric is meaningless.
- **One vote per visitor per article**, keyed by `VisitorKey` for anonymous readers and by user id for signed-in ones.
- **Archived articles keep resolving** at their URL with an archived notice — old links exist in emails and tickets.
- **Slugs are unique among live articles** and do not change when the title is edited.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/kb/categories` | `kb.view` | Tree. |
| POST/PUT/DELETE | `/api/kb/categories` | `kb.categories.manage` | |
| GET | `/api/kb/articles` | `kb.view` | Paged. Filters: `type`, `status`, `categoryId`, `isPublic`, `reviewOverdue`. |
| GET | `/api/kb/articles/{idOrSlug}` | `kb.view` | |
| POST/PUT | `/api/kb/articles` | `kb.author` | |
| POST | `/api/kb/articles/{id}/publish` , `/archive` | `kb.publish` | |
| DELETE | `/api/kb/articles/{id}` | `kb.delete` | Soft delete. |
| POST | `/api/kb/articles/{id}/feedback` | anonymous or authenticated | Helpful vote plus optional comment. |
| POST | `/api/kb/articles/{id}/view` | anonymous | Session-deduplicated view count. |
| GET | `/api/public/kb/*` | anonymous | Published public content only. |

## Backend Tasks

### 1 — Category tree and article slice

**File:** `backend/src/CustomerSupport.Application/KnowledgeBase/`

Reuse the CS-202 tree maintenance verbatim for `KbCategory` — same `Path` and `Depth` computation, same single-transaction subtree move, same self-descendant guard. Extract the shared logic into a generic helper if CS-202 has not already.

`GetKbArticlesQuery` follows the customers list pattern. **Two distinct query paths matter here:**

```csharp
// Portal callers see only published, public, non-deleted content.
if (currentUser.CustomerId is not null || !currentUser.IsAuthenticated)
{
    query = query.Where(a => a.Status == PublicationStatus.Published && a.IsPublic);
}
```

Put this in the handler, not the controller, so a future caller cannot bypass it.

Slug generation: transliterate or slugify the English title, append a numeric suffix on collision, and **never regenerate on title edit**.

### 2 — Lifecycle, voting and view counting

`PublishArticleCommand` requires `kb.publish`, sets `PublishedAt`, and snapshots a `KbArticleVersion` (the version entity is used properly in CS-602; create the first snapshot here).

Voting is idempotent per voter:

```csharp
var existing = await db.KbArticleFeedback.FirstOrDefaultAsync(f =>
    f.ArticleId == id &&
    (currentUser.UserId != null ? f.UserId == currentUser.UserId : f.VisitorKey == request.VisitorKey), ct);

if (existing is not null)
{
    // Allow changing a vote, but never counting twice.
    if (existing.IsHelpful == request.IsHelpful) return;
    ApplyDelta(article, from: existing.IsHelpful, to: request.IsHelpful);
    existing.IsHelpful = request.IsHelpful;
    existing.Comment = request.Comment;
}
else { ... }
```

View counting uses `ExecuteUpdateAsync` to increment without loading the row, and the client sends the view only once per session per article.

## Frontend Tasks

### 3 — Authoring list and editor

**File:** `frontend/src/app/features/agent/knowledge-base/`

List with columns for title (localized), type, category, status chip, public/internal, views, helpful ratio, review-due, and updated. Filters for type, status, category and review-overdue.

The FAQ editor is deliberately simple — title, summary and body per language in a side-by-side bilingual layout, category, public toggle, featured toggle, keywords — with the rich editor deferred to CS-602.

Show a completeness indicator when one language is empty, so authors see the gap rather than discovering it in the portal.

### 4 — Article view and voting

A shared article renderer used by both the agent side and the portal: title, body, last-updated, and a helpful/not-helpful footer.

On a not-helpful vote, reveal an optional comment box — the comment is where the actual signal is.

Show the language-fallback notice when the requested language is empty: "This article is not yet available in Arabic. Showing the English version."

Post the view once per session, tracked in `sessionStorage` inside a try/catch.

## Verification Steps

1. Create a category tree three levels deep and confirm paths and depths, mirroring the CS-202 behaviour.
2. Create an FAQ as a draft: it is absent from the portal.
3. Publish it: it appears in the portal under its category.
4. Mark it featured: it moves to the top.
5. View it twice in one session: the count increases once.
6. View it in a new session: the count increases again.
7. Vote helpful, then not helpful, from the same session: the counts move by one each way, never double-counting.
8. Create an article with an Arabic body only and view it in English: the Arabic is shown with the fallback notice.
9. Mark an article internal and confirm it never appears in any portal response.
10. Archive an article and open its old URL: it resolves with an archived notice.
11. Edit a published article title: the slug does not change.
12. As a portal visitor, request a draft article by id directly: 404.

## Done Criteria

- [ ] KB category tree with correct path maintenance, mirroring CS-202.
- [ ] Article CRUD with the full lifecycle behind the author, publish and delete permissions.
- [ ] Portal visibility filtering lives in the handler and cannot be bypassed.
- [ ] Language fallback shows content with a notice rather than a blank page.
- [ ] Voting is idempotent per voter; view counting is session-deduplicated.
- [ ] Slugs are stable across title edits and archived URLs still resolve.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
