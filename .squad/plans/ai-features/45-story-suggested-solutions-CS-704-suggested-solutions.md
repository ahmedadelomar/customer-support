# Story 45 — Semantic solution retrieval (Story: CS-704-suggested-solutions)

## Prerequisites

- CS-603 must be complete — this **adds to** its panel through the `Source` field rather than replacing it.
- CS-604 and CS-701 must be complete.
- If CS-603 has not landed, build it first. An AI-only panel with no rule-based fallback violates the graceful-degradation rule.

## Story Goal

Find the right article even when the customer used different words. Semantic suggestions appear beside
the rule-based ones in a single ranked list, labelled by source, and the panel keeps working when the model
does not.

## Context — Read These Files First

1. `.squad/stories/ai-features/CS-704-suggested-solutions/intake.md`.
2. [backend/src/CustomerSupport.Application/KnowledgeBase/Queries/GetSuggestedSolutionsQuery.cs](backend/src/CustomerSupport.Application/KnowledgeBase/Queries/GetSuggestedSolutionsQuery.cs) (CS-603) — the `Source` field was added for this story.
3. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticleUsage.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbArticleUsage.cs) (CS-603) — effectiveness tracking applies to AI-sourced insertions too.
4. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbSearchLog.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbSearchLog.cs) — content gaps are recorded here.

## Product rules (from story)

- **One panel, two sources.** AI and rule-based suggestions merge into a single ranked list, each labelled.
- **Rule-based results always render**, even when retrieval fails. The panel must never be empty because the model was slow.
- **Below the relevance floor, suggest nothing and record a content gap.**
- **AI-sourced insertions are tracked identically** to rule-based ones, so effectiveness comparison is apples to apples.
- **Arabic tickets retrieve Arabic articles.**

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Hybrid retrieval and reranking

Start with hybrid retrieval rather than a vector store — it needs no new infrastructure and is measurably better than keyword alone:

1. Retrieve the top 20 candidates with the existing full-text search (CS-604), which already handles Arabic normalisation.
2. Ask the model to rerank them against the ticket conversation, returning ids with relevance scores and a one-line reason each.
3. Merge with the rule-based results, deduplicate by article id (keeping the higher score and noting both sources), and take the top 5.

```csharp
// Reranking is cheap: the model sees titles and summaries, not full bodies.
var candidates = await search.SearchAsync(query, ticket.Language, top: 20, ct);

var reranked = await ai.CompleteAsync("suggested_solution", BuildRerankPrompt(ticket, candidates), ct: ct);
```

Use `effort: "low"` — reranking a shortlist does not need deep reasoning.

Document the vector-store upgrade path in the code comment, but do not build it in this story.

### 2 — Graceful merge and gap recording

```csharp
var ruleBased = await mediator.Send(new GetSuggestedSolutionsQuery(ticketId), ct);

List<SuggestedSolutionDto> aiBased = [];
try
{
    aiBased = await GetAiSuggestionsAsync(ticketId, ct);
}
catch (Exception ex)
{
    // The panel must not go empty because the model was unavailable.
    logger.LogWarning(ex, "AI solution retrieval failed for ticket {TicketId}", ticketId);
}

var merged = Merge(ruleBased, aiBased);   // dedupe by article id, keep both source labels
```

When nothing clears the relevance floor, write a `KbSearchLog` row with `ResultCount = 0` and `Source = "Agent"`, so the ticket surfaces in the same content-gap report as zero-result searches. This is the most valuable by-product of the feature: it identifies what to write next from real tickets rather than from guesses.

## Frontend Tasks

### 3 — Merged suggestions panel

Extend the CS-603 panel rather than adding a second one. Each row gains a small source label — **Category match** or **AI match** — with the AI reason on hover.

The panel renders as soon as rule-based results arrive and appends AI results when they land, so a slow model call never delays the panel.

Show a subtle "no matching solutions" state with a link to write one, rather than an empty box.

## Verification Steps

1. Open a ticket whose wording differs from the article title but means the same: the article is suggested by the AI source.
2. Confirm rule-based suggestions still appear alongside, each labelled.
3. Confirm an article matched by both sources appears once, with both labels.
4. Insert an AI-suggested solution and resolve the ticket: usage and the resolution credit are recorded exactly as for a rule-based one.
5. Disable the AI feature: the panel still shows rule-based suggestions.
6. Make the model call fail: the panel renders rule-based results and logs the failure.
7. Make the model call slow: the panel renders immediately and appends the AI results later.
8. Open a ticket with no relevant articles: nothing is suggested and a content gap is recorded.
9. Confirm the gap appears in the content-gap report beside zero-result searches.
10. Open an Arabic ticket: Arabic articles are retrieved and ranked.

## Done Criteria

- [ ] Hybrid retrieval reranks full-text candidates; no vector infrastructure is required.
- [ ] AI and rule-based suggestions merge into one deduplicated, labelled list.
- [ ] The panel renders rule-based results immediately and never depends on the model.
- [ ] AI-sourced insertions are tracked identically for effectiveness.
- [ ] Tickets with no good match are recorded as content gaps.
- [ ] Arabic retrieval works.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
