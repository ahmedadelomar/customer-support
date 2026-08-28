# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/knowledge-base/CS-604-kb-search/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 6 — Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-604-kb-search`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `knowledge-base, search`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Search
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer or agent,
I want to find the right article quickly by typing what I mean,
So that the knowledge base is actually usable rather than just present.

Scope:
- Full-text search across titles, summaries, bodies and keywords, in both languages.
- Arabic-aware matching that tolerates spelling variants.
- Relevance ranking with field weighting.
- Filters by category, type and language.
- Search-as-you-type suggestions.
- Search logging, with zero-result queries driving a content-gap report.
- Agent search from within the ticket screen.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I search,
Then results match against title, summary, body and keywords,
And title matches rank above body matches.

Given I search in Arabic using a spelling variant such as أحمد versus احمد,
Then both forms match, because alef and taa-marbuta variants are normalised.

Given I search,
Then only published articles visible to me are returned,
And internal articles never appear for a portal visitor.

Given I filter by category or type,
Then results narrow accordingly.

Given I type at least three characters,
Then suggestions appear as I type without a full page load.

Given every search,
Then the query, normalised form, language, result count and source are logged.

Given I click a result,
Then the article and its rank are recorded, so relevance can be tuned with real data.

Given a search returns nothing,
Then it is highlighted in the content-gap report,
And the report groups similar queries by their normalised form.

Given the content-gap report,
Then authors see the most frequent zero-result queries with their counts,
So they know what to write next.

Given search performance,
Then results return quickly on a knowledge base of thousands of articles.
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

- CS-601 and CS-602 for content to search.
- `KbSearchLog` entity — already defined, with the zero-result index.
- CS-1201 for the Arabic normalisation shared with customer search.
- SQL Server full-text search, or a dedicated search index.

## Extra notes (optional)

- Zero-result queries are the single most valuable output of a knowledge base: they are customers telling you exactly what is missing.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Arabic full-text search needs both a normalised stored column and a normalised query term. Normalising only one side silently fails.

## Out of scope

- What this story explicitly does **not** cover:

- Semantic or vector search — a possible follow-up once the AI feature set is in place.
- Search across tickets and customers; this is knowledge-base search only.
