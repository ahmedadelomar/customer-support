# Story 43 — Grounded suggested replies (Story: CS-702-suggested-replies)

## Prerequisites

- CS-701 must be complete.
- CS-404 (quick replies) must be complete — this reuses its placeholder resolver.
- CS-604 (knowledge base search) must be complete — replies are grounded in retrieved articles.

## Story Goal

Agents get a draft reply grounded in real knowledge base content, with the sources shown so a wrong
retrieval is visible. Nothing is ever sent without a human reading it.

## Context — Read These Files First

1. `.squad/stories/ai-features/CS-702-suggested-replies/intake.md`.
2. [backend/src/CustomerSupport.Infrastructure/Ai/AnthropicCompletionService.cs](backend/src/CustomerSupport.Infrastructure/Ai/AnthropicCompletionService.cs) (CS-701).
3. [backend/src/CustomerSupport.Application/Workspace/QuickReplies/PlaceholderResolver.cs](backend/src/CustomerSupport.Application/Workspace/QuickReplies/PlaceholderResolver.cs) (CS-404) — reuse, do not reimplement.
4. [backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs](backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs) — `AiSuggestionId` links a sent message to the suggestion it came from.

## Product rules (from story)

- **Ground every reply in retrieved articles.** An ungrounded model invents refund policies and SLA promises.
- **Cite the articles used** so an agent can spot a wrong retrieval before sending.
- **Nothing is ever sent automatically.** The draft lands in the composer as editable text.
- **Below the confidence floor, show nothing.**
- **Unresolved placeholders render as visible markers**, exactly as quick replies do.
- **Reply in the customer's language**, not the agent's.
- **Accept, edit and reject rates are the measurement** of whether this feature earns its cost.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Retrieval and prompt construction

**File:** `backend/src/CustomerSupport.Application/Ai/Commands/GenerateSuggestedReplyCommand.cs`

Retrieve first, then generate. Order the prompt so the cacheable part comes first:

```csharp
// 1. Retrieve grounding from the knowledge base using the ticket subject and last customer message.
var articles = await search.SearchAsync(
    query: $"{ticket.Subject} {lastCustomerMessage}",
    language: ticket.Language, top: 3, publishedOnly: true, ct);

// 2. Stable system prompt (cached) — tone, rules, output language.
// 3. Volatile user prompt — the articles and the conversation.
var prompt = new StringBuilder()
    .AppendLine("## Knowledge base articles")
    .AppendLine(string.Join("\n---\n", articles.Select(a =>
        $"[{a.Id}] {a.Title}\n{StripHtml(a.Body)}")))
    .AppendLine("## Conversation")
    .AppendLine(conversationText)
    .ToString();
```

The system prompt must state: reply in `{ticket.Language}`; use only the supplied articles for factual claims; if the articles do not cover the question, say so rather than guessing; cite article ids used; do not promise timelines or refunds not stated in the articles.

Store the retrieved article ids on the suggestion so the citation list survives.

When retrieval returns nothing, still generate — but instruct the model to acknowledge the limits and suggest escalation rather than inventing an answer.

### 2 — Placeholder resolution and confidence gating

Run the generated text through `PlaceholderResolver` so any `{{customer.displayName}}` the model emits resolves exactly as a quick reply would, and unresolved tokens surface as visible markers.

Gate on confidence:

```csharp
if (result.Confidence is { } c && c < config.MinConfidence)
{
    suggestion.Status = AiSuggestionStatus.Expired;
    await db.SaveChangesAsync(ct);
    return null;   // a weak suggestion costs more attention than none
}
```

On send, stamp `TicketMessage.AiSuggestionId` and mark the suggestion `Accepted` or `Edited` by comparing the sent text to the generated text.

## Frontend Tasks

### 3 — Suggest reply in the composer

A **Suggest a reply** button beside the quick reply picker. While generating, show an inline skeleton in the composer rather than a modal — the agent should be able to start typing and abandon the suggestion.

When it arrives, insert as editable text and show a dismissible banner above the composer listing the cited articles as links, plus an **AI-drafted — please review** label that stays until the agent edits or sends.

If unresolved placeholders exist, warn as CS-404 does.

Never auto-send. There is no configuration that enables auto-send.

## Verification Steps

1. Generate a reply on a ticket whose answer is covered by an article: the draft uses the article content and cites it.
2. Follow a citation link: it opens the article the model actually used.
3. Generate on a ticket with no relevant article: the draft acknowledges the limit rather than inventing a policy.
4. Confirm the draft is in the customer's language even when the agent interface is the other language.
5. Confirm the draft contains no promise of a refund or timeline absent from the articles.
6. Send unchanged: recorded as Accepted, and the message carries `AiSuggestionId`.
7. Edit then send: recorded as Edited with both versions stored.
8. Dismiss: recorded as Rejected.
9. Set the confidence floor above what the model returns: no suggestion appears.
10. Break the search service: generation still works, ungrounded, and says so.
11. Break the model call: the composer works normally.
12. Confirm there is no code path that sends a suggested reply without an agent action.

## Done Criteria

- [ ] Replies are grounded in retrieved articles with citations shown to the agent.
- [ ] Output is in the customer's language and free of unsupported factual claims.
- [ ] Placeholders resolve through the shared CS-404 resolver.
- [ ] Confidence gating suppresses weak suggestions.
- [ ] Accept, edit and reject are recorded, and sent messages are attributed.
- [ ] Retrieval or model failure degrades gracefully.
- [ ] No auto-send path exists.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
