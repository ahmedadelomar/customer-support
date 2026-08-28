# knowledge-base — plan overview

Entry point for the **Section 6 — Knowledge Base** feature. Stories execute in order by their `NN` prefix.

Self-service content that also makes agents faster. One article entity serves FAQs, help articles,
solutions and guides, because they share authoring, search, feedback and analytics. Story 36 (search) is what
turns a content store into something people actually use.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 33 | [33-story-faqs-CS-601-faqs.md](33-story-faqs-CS-601-faqs.md) | Knowledge base foundation and FAQs | CS-601-faqs | 01 |
| 34 | [34-story-help-articles-CS-602-help-articles.md](34-story-help-articles-CS-602-help-articles.md) | Rich authoring, versioning and review lifecycle | CS-602-help-articles | 33 |
| 35 | [35-story-solutions-guides-CS-603-solutions-guides.md](35-story-solutions-guides-CS-603-solutions-guides.md) | Solutions linked to categories, and step guides | CS-603-solutions-guides | 34 |
| 36 | [36-story-kb-search-CS-604-kb-search.md](36-story-kb-search-CS-604-kb-search.md) | Bilingual full-text search and the content-gap report | CS-604-kb-search | 34 |

## Dependency notes

- **Story 33 establishes the shared model.** Stories 34–36 extend it rather than adding parallel entities. Resist the urge to give guides their own table.
- Story 36 (search) depends on the Arabic normalisation introduced in CS-1201. Both sides — the stored column and the query term — must use the same normaliser, or Arabic search silently fails.
- Story 35 (solutions) defines the suggestion panel contract that **CS-704 (AI suggested solutions) plugs into**. Keep the panel and its DTO stable so the AI version slots in beside the rule-based one rather than replacing the screen.
- Article images reuse the attachment store from CS-104. Do not add a second upload mechanism.
- The portal surfaces (CS-804) render this content. These stories own authoring, the API and agent-side use; the portal story owns the customer-facing pages.
