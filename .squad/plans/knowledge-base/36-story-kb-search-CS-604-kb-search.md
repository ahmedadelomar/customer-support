# Story 36 — Bilingual full-text search and the content-gap report (Story: CS-604-kb-search)

## Prerequisites

- CS-601 and CS-602 must be complete.
- CS-1201 must be complete — this story reuses its Arabic normaliser.
- SQL Server full-text indexing must be installed on the database instance; confirm before starting, since it is not enabled by default on every edition.

## Story Goal

People find what they need. Search covers both languages, tolerates Arabic spelling variation, ranks
title matches above body matches, and logs every query — because zero-result queries are customers telling
you exactly what is missing.

## Context — Read These Files First

1. `.squad/stories/knowledge-base/CS-604-kb-search/intake.md`.
2. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbSearchLog.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbSearchLog.cs) — read the class remark: this is persisted specifically to drive the gap report.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — the `(ResultCount, NormalizedQuery)` index the gap report groups on.
4. The Arabic normaliser from CS-1201 — the same one used for customer search.

## Product rules (from story)

- **Normalise both sides.** The stored searchable column and the query term must go through the same normaliser, or Arabic search silently returns nothing.
- **Field weighting:** title above summary above keywords above body. A body mention is not as good as a title match.
- **Visibility filtering applies before ranking.** A portal visitor must never see an internal article, even at rank 50.
- **Every query is logged**, including the click-through rank, which is what makes relevance tunable with evidence.
- **The gap report groups by normalised query**, so ten spellings of the same question appear as one gap.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/kb/search` | `kb.view` | Agent search: all visible content. |
| GET | `/api/public/kb/search` | anonymous | Published public content only. |
| GET | `/api/kb/search/suggest` | either | Type-ahead, minimum 3 characters. |
| POST | `/api/kb/search/click` | either | Records the clicked article and its rank. |
| GET | `/api/kb/search/gaps` | `kb.analytics.view` | Zero-result queries grouped and counted. |

## Backend Tasks

### 1 — Searchable column and full-text index

**File:** migration plus `backend/src/CustomerSupport.Infrastructure/Persistence/`

Add a persisted normalised column per language and a full-text index over them. Normalising at write time means the query only has to normalise the term:

```sql
ALTER TABLE KbArticles ADD SearchTextEn NVARCHAR(MAX) NULL;
ALTER TABLE KbArticles ADD SearchTextAr NVARCHAR(MAX) NULL;

CREATE FULLTEXT CATALOG KbCatalog AS DEFAULT;
CREATE FULLTEXT INDEX ON KbArticles (SearchTextEn LANGUAGE 1033, SearchTextAr LANGUAGE 1025)
    KEY INDEX PK_KbArticles WITH CHANGE_TRACKING AUTO;
```

Populate the columns in the command handler on every save, concatenating title, summary, keywords and a tag-stripped body, then applying the Arabic normaliser to the Arabic column.

```csharp
article.SearchTextAr = ArabicNormalizer.Normalize(
    $"{article.Title.Ar} {article.Summary.Ar} {article.Keywords} {StripHtml(article.Body.Ar)}");
```

The Arabic normaliser folds alef variants (أ إ آ to ا), taa marbuta (ة to ه), alef maqsura (ى to ي), tatweel and diacritics.

### 2 — Ranked search query

Use `CONTAINSTABLE` for the rank, and apply visibility **before** ranking so an excluded article cannot occupy a slot:

```csharp
var normalized = lang == "ar" ? ArabicNormalizer.Normalize(term) : term.ToLowerInvariant();
var column = lang == "ar" ? "SearchTextAr" : "SearchTextEn";

var results = await db.KbArticles
    .FromSqlRaw($@"
        SELECT a.* FROM KbArticles a
        INNER JOIN CONTAINSTABLE(KbArticles, {column}, {{0}}) ft ON a.Id = ft.[KEY]
        ORDER BY
            -- Title matches outrank body matches regardless of raw full-text rank.
            CASE WHEN a.Title{(lang == "ar" ? "Ar" : "En")} LIKE {{1}} THEN 0 ELSE 1 END,
            ft.RANK DESC", searchExpression, $"%{term}%")
    .Where(visibilityPredicate)
    .Take(request.PageSize)
    .ToListAsync(ct);
```

Escape the term for full-text syntax — an unescaped quote or `AND` breaks the query or changes its meaning.

Fall back to `LIKE` matching when full-text is unavailable on the instance, logging a warning once at startup rather than failing.

### 3 — Logging and the gap report

Log every search: query, normalised form, language, result count, source (`Portal`, `Agent` or `Chatbot`), duration, and the caller.

`POST /api/kb/search/click` records `ClickedArticleId` and `ClickedRank` against the most recent log row for that session — click rank is the raw material for tuning relevance later.

The gap report:

```csharp
var gaps = await db.KbSearchLogs
    .Where(l => l.ResultCount == 0 && l.SearchedAt >= from)
    .GroupBy(l => new { l.NormalizedQuery, l.Language })
    .Select(g => new SearchGapDto
    {
        Query = g.OrderByDescending(x => x.SearchedAt).First().Query,   // a real spelling to read
        NormalizedQuery = g.Key.NormalizedQuery,
        Language = g.Key.Language,
        Count = g.Count(),
        LastSearchedAt = g.Max(x => x.SearchedAt),
    })
    .OrderByDescending(g => g.Count)
    .Take(100)
    .ToListAsync(ct);
```

Grouping by the normalised form is what collapses ten spellings of one question into a single actionable gap.

## Frontend Tasks

### 4 — Search UI with type-ahead

A search box on the portal and in the agent knowledge base, with results showing title, category, type chip and a highlighted snippet.

Type-ahead after three characters, debounced at 250ms, showing up to five suggestions. Keyboard navigable.

Filters for category, type and language. Clicking a result posts the click with its rank before navigating.

An empty-results state that offers to open a ticket instead — a customer who searched and found nothing is exactly who should be offered support.

### 5 — Agent search in the ticket screen

A search panel beside the composer, so an agent never leaves the ticket. Results have **Preview** and **Insert link** actions, the latter appending a link to the public article in the ticket language.

Pre-fill the search with the ticket subject on first open — usually the right query, and it saves retyping.

### 6 — Content-gap report

An authoring dashboard section listing top zero-result queries with counts, language and last-searched date, filterable by date range and language, with a **Write an article** action that opens the editor pre-filled with the query as the working title.

## Verification Steps

1. Search an English term appearing in a title and another appearing only in a body: the title match ranks first.
2. Search "احمد" for an article containing "أحمد": it matches, proving both sides are normalised.
3. Search with diacritics and with tatweel: both match the same article.
4. As a portal visitor, search a term that appears in an internal article: it is absent from the results at any rank.
5. Search a term containing a quote character: the query does not error.
6. Type three characters: suggestions appear within the debounce window.
7. Click a result: the click and its rank are recorded on the log row.
8. Search a nonsense term: zero results, the empty state offers to open a ticket, and the query is logged.
9. Search the same nonsense term with three spellings: the gap report shows one grouped entry with a count of three.
10. Open the gap report and click "Write an article": the editor opens with the query as the title.
11. Search a knowledge base of several thousand articles: results return quickly.
12. Disable full-text on the instance: search falls back to LIKE with a startup warning rather than failing.

## Done Criteria

- [ ] Persisted normalised search columns with a full-text index, populated on save.
- [ ] Arabic normalisation applied to both the stored column and the query term.
- [ ] Title matches outrank body matches; visibility filtering precedes ranking.
- [ ] Type-ahead, filters and click-rank logging all work.
- [ ] Every search is logged with its source and duration.
- [ ] The gap report groups by normalised query and links into authoring.
- [ ] A documented fallback exists when full-text is unavailable.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
