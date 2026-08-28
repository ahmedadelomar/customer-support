# Story 35 — Solutions linked to categories, and step guides (Story: CS-603-solutions-guides)

## Prerequisites

- CS-601 and CS-602 must be complete.
- CS-201 must be complete for the ticket screen the suggestion panel lives on.
- **The panel contract defined here is what CS-704 plugs into.** Design the DTO so an AI-ranked list can populate the same component.

## Story Goal

The right solution appears on the right ticket without anyone searching. Solutions are associated with
ticket categories, ranked by whether they actually resolve tickets, and inserted into replies in one action.

## Context — Read These Files First

1. `.squad/stories/knowledge-base/CS-603-solutions-guides/intake.md`.
2. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticle.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticle.cs) — `RelatedCategoryIds` and `UsedInReplyCount` exist for exactly this.
3. [backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs](backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs) — records which article or quick reply produced a message.
4. [frontend/src/app/features/agent/quick-replies/](frontend/src/app/features/agent/quick-replies/) (CS-404) — the composer insertion pattern to reuse.

## Product rules (from story)

- **Rank by effectiveness, not recency or raw insertions.** Effectiveness is resolutions-after-insertion divided by insertions.
- **A solution inserted then followed by resolution counts as successful.** Insertion alone counts only as a use.
- **Insert the body in the ticket language**, exactly as quick replies do.
- **Guide step progress is per visitor and stored locally**, never server-side — it is a reading aid, not data.
- **The coverage report ranks gaps by ticket volume**, so authors write what matters most.

## Data model

Add one entity for effectiveness, and one for guide steps:

```csharp
// backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticleUsage.cs
public class KbArticleUsage : BaseEntity
{
    public Guid ArticleId { get; set; }
    public Guid TicketId { get; set; }
    public Guid TicketMessageId { get; set; }
    public Guid InsertedById { get; set; }
    public DateTimeOffset InsertedAt { get; set; }

    /// <summary>Set when the ticket reached a resolved status after this insertion.</summary>
    public DateTimeOffset? ResolvedAfterAt { get; set; }
}

// backend/src/CustomerSupport.Domain/KnowledgeBase/KbGuideStep.cs
public class KbGuideStep : BaseEntity
{
    public Guid ArticleId { get; set; }
    public int Order { get; set; }
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Body { get; set; } = new();
}
```

Indexes: `(ArticleId, InsertedAt)` and `(TicketId)` on usage; `(ArticleId, Order)` unique on steps.

Storing usage as rows rather than only incrementing a counter is what makes the outcome link possible.

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Suggestion query

**File:** `backend/src/CustomerSupport.Application/KnowledgeBase/Queries/GetSuggestedSolutionsQuery.cs`

```csharp
var categoryId = ticket.CategoryId.ToString();

var candidates = db.KbArticles
    .Where(a => a.Status == PublicationStatus.Published
                && a.Type == KbArticleType.Solution
                && a.RelatedCategoryIds != null
                && a.RelatedCategoryIds.Contains(categoryId));

// Effectiveness: resolutions after insertion over insertions, with a floor so a single
// lucky insertion does not outrank a well-proven article.
var ranked = await candidates
    .Select(a => new
    {
        Article = a,
        Uses = db.KbArticleUsages.Count(u => u.ArticleId == a.Id),
        Resolved = db.KbArticleUsages.Count(u => u.ArticleId == a.Id && u.ResolvedAfterAt != null),
    })
    .Select(x => new SuggestedSolutionDto
    {
        ...,
        Effectiveness = x.Uses < 5 ? 0.5m : (decimal)x.Resolved / x.Uses,   // neutral prior
        UseCount = x.Uses,
        Source = "category",   // CS-704 will emit "ai" alongside these
    })
    .OrderByDescending(x => x.Effectiveness)
    .ThenByDescending(x => x.UseCount)
    .Take(5)
    .ToListAsync(ct);
```

The neutral prior for low-use articles is deliberate: without it, one article used once and resolved once ranks above one used 200 times with an 80 percent rate.

**Include a `Source` field in the DTO now.** CS-704 will add AI-ranked entries to the same panel, and a stable contract means it does not have to rebuild the UI.

### 2 — Usage tracking and outcome linking

When an agent inserts a solution into a reply, record a `KbArticleUsage` row and increment `UsedInReplyCount`.

When a ticket reaches a resolved status (CS-204), stamp the outcome on recent insertions:

```csharp
// Attribute the resolution to solutions inserted on this ticket that have not yet been credited.
await db.KbArticleUsages
    .Where(u => u.TicketId == ticketId && u.ResolvedAfterAt == null)
    .ExecuteUpdateAsync(s => s.SetProperty(u => u.ResolvedAfterAt, now), ct);
```

Add this call to `ChangeTicketStatusCommand`'s resolved branch. Note the dependency in the CS-204 plan so it is not missed.

### 3 — Guide steps and the coverage report

CRUD for `KbGuideStep` nested under the article, with reordering that rewrites `Order` in one transaction.

`GET /api/kb/coverage` returns ticket categories with no published solution, joined to ticket volume over the last 90 days, ordered by volume descending. This is the report that tells authors what to write next, and it is far more useful ordered by volume than alphabetically.

## Frontend Tasks

### 4 — Suggested solutions panel

**File:** `frontend/src/app/features/agent/tickets/panels/suggested-solutions.component.ts`

A collapsible panel on the ticket screen listing up to five suggestions with title, an effectiveness indicator and use count. Actions per row: **Preview** (a side panel showing the article) and **Insert into reply**.

Group by `source` with a subtle label, so when CS-704 adds AI suggestions the agent can tell which is which without the panel being redesigned.

Insertion appends the article body in the ticket language plus a link to the public article, and records the usage.

### 5 — Guide authoring and rendering

In the editor, a guide-type article gains a steps section: an ordered list of step cards, each with bilingual title and body, drag to reorder, add and remove.

The rendered guide shows numbered steps with a checkbox each, progress stored in `localStorage` keyed by article id inside a try/catch, and a progress bar. Nothing is sent to the server — it is a reading aid.

### 6 — Coverage report

An authoring dashboard section listing uncovered categories with their ticket volume and a **Write a solution** action that opens the editor pre-filled with that category. Removing friction between seeing the gap and filling it is the point.

## Verification Steps

1. Associate a solution with a ticket category and open a ticket in it: the solution is suggested.
2. Insert it into a reply: the body appears in the ticket language and a usage row is recorded.
3. Resolve the ticket: the usage row gains a resolution timestamp.
4. Create two solutions in one category, one with a high resolution rate over many uses and one used once successfully: the well-proven one ranks first, proving the neutral prior works.
5. Confirm the suggestion DTO carries a `source` field.
6. Create a guide with five steps: they render numbered with checkboxes.
7. Tick some steps, reload the page: progress is preserved locally.
8. Confirm nothing about step progress is sent to the server.
9. Open the coverage report: categories without solutions are listed, ordered by ticket volume.
10. Click "Write a solution" from the report: the editor opens with the category pre-selected.
11. Insert a solution on an Arabic ticket while the interface is English: the Arabic body is inserted.

## Done Criteria

- [ ] Solutions are associable with ticket categories and suggested on matching tickets.
- [ ] Ranking uses effectiveness with a neutral prior for low-use articles.
- [ ] Usage rows are recorded and credited on resolution, wired into CS-204.
- [ ] The suggestion DTO carries `source`, ready for CS-704.
- [ ] Guides support ordered steps with client-side progress only.
- [ ] The coverage report ranks gaps by ticket volume and links into authoring.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
