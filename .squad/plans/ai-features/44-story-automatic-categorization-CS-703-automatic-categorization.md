# Story 44 — Automatic ticket categorisation and priority suggestion (Story: CS-703-automatic-categorization)

## Prerequisites

- CS-701 must be complete.
- CS-201 and CS-202 must be complete.
- Classification runs on ticket creation, so it must not block or fail that path.

## Story Goal

Tickets arrive classified even when customers pick the wrong category or none. High-confidence
results apply automatically; the rest are suggestions. Accuracy is measured against what agents actually chose,
so the feature can be judged rather than assumed.

## Context — Read These Files First

1. `.squad/stories/ai-features/CS-703-automatic-categorization/intake.md`.
2. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — `AiCategoryConfidence` exists for this.
3. [backend/src/CustomerSupport.Application/Tickets/Commands/CreateTicketCommand.cs](backend/src/CustomerSupport.Application/Tickets/Commands/CreateTicketCommand.cs) (CS-201) — where classification hooks in, **after** the commit.
4. [backend/src/CustomerSupport.Domain/Ai/AiSuggestion.cs](backend/src/CustomerSupport.Domain/Ai/AiSuggestion.cs) — `SuggestedCategoryId` and `SuggestedPriorityId`.

## Product rules (from story)

- **Classification never blocks ticket creation.** It runs after the commit, and its failure is logged, not surfaced.
- **Automatic application only above the configured confidence threshold**, and always recorded as an automation event naming the confidence.
- **Agent overrides are the ground truth** for accuracy measurement.
- **The category list is read live**, so a newly added category is classifiable immediately.
- **Structured output, not prose.** Ask for a category id and confidence, and validate the id exists before applying it.
- **Sentiment informs priority as a suggestion**, never an automatic escalation — an angry customer is not always an urgent ticket.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Classification with structured output

**File:** `backend/src/CustomerSupport.Application/Ai/Commands/ClassifyTicketCommand.cs`

Present the live category list in the prompt and require a structured response:

```csharp
var categories = await db.TicketCategories
    .Where(c => c.IsActive)
    .Select(c => new { c.Id, c.Code, NameEn = c.Name.En, NameAr = c.Name.Ar, c.Description })
    .ToListAsync(ct);
```

Ask for JSON and validate it — never trust the id blindly:

```csharp
var parsed = JsonSerializer.Deserialize<ClassificationResult>(result.Content);

// The model can return a plausible-looking id that does not exist.
if (parsed is null || !categories.Any(c => c.Id == parsed.CategoryId))
{
    logger.LogWarning("Classifier returned an unknown category for ticket {TicketId}", ticketId);
    return;
}
```

`ClassificationResult` carries `CategoryId`, `Confidence` (0–1), `SuggestedPriorityCode`, `Sentiment` and a short `Reasoning` string for the UI.

Use `effort: "low"` — classification is the cheapest possible task and does not need deep reasoning.

Apply automatically above the threshold, recording the automation:

```csharp
if (parsed.Confidence >= config.MinConfidence && config.RunAutomatically)
{
    ticket.CategoryId = parsed.CategoryId;
    ticket.AiCategoryConfidence = parsed.Confidence;

    events.Record(ticket.Id, TicketEventType.CategoryChanged,
        oldDisplay: oldName, newDisplay: newName,
        triggeredByRule: $"AI classification ({parsed.Confidence:P0} confidence)");
}
```

### 2 — Accuracy measurement and batch reclassification

Accuracy comes from comparing the suggestion to what an agent later chose. When `ChangeTicketCategoryCommand` runs on a ticket with a pending or applied classification, record the agent's choice against the suggestion — agreement or correction.

`GET /api/ai/classification-accuracy` returns overall agreement plus a per-category breakdown, ordered by volume, so a manager can see which categories the model handles badly rather than only an aggregate number.

Batch reclassification is a background job over a filtered ticket set, reporting progress and cancellable, writing suggestions **without** applying them — a bulk automatic recategorisation of history would corrupt reporting.

### 3 — Wire into creation without blocking it

```csharp
await db.SaveChangesAsync(ct);   // the ticket exists and is safe

// Classification is best-effort. A slow or failed model call must never
// delay or fail a customer's ticket.
_ = Task.Run(async () =>
{
    try { await mediator.Send(new ClassifyTicketCommand(ticket.Id), CancellationToken.None); }
    catch (Exception ex) { logger.LogWarning(ex, "Classification failed for {TicketId}", ticket.Id); }
});
```

Prefer dispatching through the outbox (CS-504) rather than a bare `Task.Run`, so a process restart does not lose the classification. Note the trade-off in the code comment either way.

## Frontend Tasks

### 4 — Classification indicator and accuracy report

On the ticket properties sidebar, when a category was set by AI, show a small badge with the confidence and the model's short reasoning on hover. Changing the category clears the badge and records the correction.

When confidence was below the threshold, show the suggestion as an inline prompt beside the category field — "Suggested: Billing (62%)" with **Apply** and **Dismiss** — rather than applying it.

The accuracy report lives on the AI admin screen: overall agreement, a per-category table with volume and agreement, and a list of the most-corrected categories with example tickets.

## Verification Steps

1. Create a ticket with a clearly billing-related description: it is classified as Billing with high confidence and applied automatically.
2. Confirm a ticket event records the automation and the confidence.
3. Create an ambiguous ticket: the suggestion appears but is not applied.
4. Have an agent correct an AI-applied category: the correction is recorded.
5. Open the accuracy report: agreement rate and the per-category breakdown are populated.
6. Add a new category and create a matching ticket: it is classifiable immediately.
7. Force the model to return a non-existent category id: it is rejected and logged, and the ticket keeps its default.
8. Make the model call hang: ticket creation still completes promptly.
9. Make the model call fail: the ticket is created with the default category.
10. Create a ticket with an angry message: a higher priority is suggested with the reason, but not applied automatically.
11. Run batch reclassification over 100 tickets: progress reports, it can be cancelled, and no category is changed automatically.

## Done Criteria

- [ ] Classification runs after commit and never blocks or fails ticket creation.
- [ ] Structured output is validated against the live category list before use.
- [ ] Automatic application is threshold-gated and recorded as an automation event.
- [ ] Agent corrections are captured and drive a per-category accuracy report.
- [ ] Sentiment produces a priority suggestion, never an automatic change.
- [ ] Batch reclassification suggests without applying, and is cancellable.
- [ ] Low effort is used, keeping per-ticket cost minimal.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
