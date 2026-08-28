# Story 42 — AI service foundation and ticket summaries (Story: CS-701-ticket-summaries)

## Prerequisites

- CS-201 must be complete.
- CS-1004 should be complete for the `ai.enabled` switch; otherwise default it to off.
- An Anthropic API key is required. Store it as a secret system setting or in user-secrets — never in `appsettings.json`.
- **Read the Claude API facts below before writing any model call.** Several long-standing patterns now return a 400.

## Story Goal

Build the AI foundation — one service, one configuration entity, one review flow, one cost ledger —
and prove it with the simplest useful feature: summarising a long ticket for an agent picking it up.

Everything the rest of this section needs is established here.

## Context — Read These Files First

1. `.squad/stories/ai-features/CS-701-ticket-summaries/intake.md`.
2. [backend/src/CustomerSupport.Domain/Ai/AiSuggestion.cs](backend/src/CustomerSupport.Domain/Ai/AiSuggestion.cs) — read the class remark: suggestions are advisory, and the entity carries provenance and cost for exactly this reason.
3. [backend/src/CustomerSupport.Domain/Ai/AiModelConfig.cs](backend/src/CustomerSupport.Domain/Ai/AiModelConfig.cs) — note `Effort` rather than `Temperature`, and `MinConfidence`, `RunAutomatically` and `DailyRequestLimit`.
4. [backend/src/CustomerSupport.Application/Common/Interfaces/IAiCompletionService.cs](backend/src/CustomerSupport.Application/Common/Interfaces/IAiCompletionService.cs) — the contract to implement, including `AiCompletionResult` with its token and latency fields.
5. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — `AiSummary` holds the latest accepted summary; history stays in `AiSuggestion`.

## Product rules (from story)

- **Suggestions are advisory.** Nothing reaches a ticket, a customer or a report until an agent accepts it.
- **Every generation is logged** with model, prompt version, token counts, latency and a hash of the source content.
- **Staleness is detected by hashing the source.** If the conversation changed since generation, the summary is marked stale rather than shown as current.
- **Below `MinConfidence`, show nothing.** A bad suggestion costs more attention than no suggestion.
- **Failure is non-fatal, always.** Catch, log, and return no suggestion.
- **`DailyRequestLimit` is enforced per feature per branch**, and refusal is explicit rather than silent.
- **Edited text is stored beside the original**, because the difference is the measurement.

## Data model

`AiSuggestion` and `AiModelConfig` already exist. Note that `AiModelConfig.Temperature` was
replaced by `Effort` — the sampling parameters are rejected by current models.

Seed one `AiModelConfig` row per feature with `IsEnabled = false`, so enabling AI is a deliberate act.

## Claude API facts this feature depends on

These were verified against the current API. Several differ from older patterns that may look familiar:

- **Model ids carry no date suffix.** Use `claude-opus-5` (the default for this product), `claude-sonnet-5`, or `claude-haiku-4-5`. Never `claude-sonnet-5-20251101` or similar.
- **Sampling parameters are gone.** `temperature`, `top_p` and `top_k` are **rejected with a 400** on Opus 5, Sonnet 5 and the 4.7/4.8 family. This is why `AiModelConfig` carries `Effort` rather than `Temperature`.
- **Thinking is adaptive.** Send `thinking: { type: "adaptive" }` or omit it (Opus 5 thinks by default). `budget_tokens` is **rejected with a 400** on current models.
- **Effort replaces the old knobs:** `output_config.effort` accepts `low`, `medium`, `high`, `xhigh`, `max`. Use `low` for classification, `medium` for summaries and replies, `high` where correctness matters.
- **Assistant prefill is removed** — it returns a 400. Use structured outputs or system-prompt instructions to shape the response.
- **Stream anything long.** Non-streaming requests risk HTTP timeouts at high `max_tokens`.
- **Use the official `Anthropic` NuGet package**, not raw HTTP. `AnthropicClient` lives in `Anthropic`; request types in `Anthropic.Models.Messages` (or `Anthropic.Models.Beta.Messages` for beta paths).
- **Prompt caching is a prefix match.** Put the stable system prompt and knowledge base context first, the volatile ticket content last, or the cache never hits.

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/api/tickets/{id}/ai/summary` | `ai.suggestions.use` | Generates or returns the current suggestion. |
| GET | `/api/tickets/{id}/ai/suggestions` | `ai.suggestions.use` | Latest per type, with staleness. |
| POST | `/api/ai/suggestions/{id}/accept` | `ai.suggestions.use` | Body: optional `editedContent`. |
| POST | `/api/ai/suggestions/{id}/reject` | `ai.suggestions.use` | Body: optional `reason`. |
| GET/PUT | `/api/ai/config` | `ai.configure` | Per-feature model configuration. |

## Backend Tasks

### 1 — Anthropic completion service

**File:** `backend/src/CustomerSupport.Infrastructure/Ai/AnthropicCompletionService.cs`

Add the `Anthropic` NuGet package to Infrastructure, then implement `IAiCompletionService`:

```csharp
using Anthropic;
using Anthropic.Models.Messages;

public class AnthropicCompletionService(
    AnthropicClient client,
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<AnthropicCompletionService> logger) : IAiCompletionService
{
    public async Task<AiCompletionResult> CompleteAsync(
        string featureKey, string userPrompt, string? systemPromptOverride = null,
        CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(featureKey, ct)
            ?? throw new ConflictException($"No AI configuration exists for '{featureKey}'.");

        if (!config.IsEnabled) throw new ConflictException($"The '{featureKey}' AI feature is disabled.");
        await EnforceDailyLimitAsync(config, ct);

        var sw = Stopwatch.StartNew();

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = config.ModelId,                    // exact id, no date suffix
            MaxTokens = config.MaxTokens,

            // Stable prefix first so prompt caching can hit; the volatile ticket text
            // goes in the user message, after the cache breakpoint.
            System = [new TextBlockParam
            {
                Text = systemPromptOverride ?? config.SystemPrompt ?? DefaultSystemPrompt(featureKey),
                CacheControl = new CacheControlEphemeral(),
            }],

            Messages = [new() { Role = Role.User, Content = userPrompt }],

            // Adaptive thinking plus an effort level. Do NOT send temperature, top_p or
            // budget_tokens — current models reject all three with a 400.
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = ParseEffort(config.Effort) },
        }, cancellationToken: ct);

        sw.Stop();

        var text = string.Concat(response.Content
            .Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

        return new AiCompletionResult(
            Content: text,
            Provider: config.Provider,
            ModelId: config.ModelId,
            PromptTokens: response.Usage.InputTokens,
            CompletionTokens: response.Usage.OutputTokens,
            LatencyMs: (int)sw.ElapsedMilliseconds,
            Confidence: null);
    }
}
```

Register `AnthropicClient` as a singleton reading the key from configuration. Handle `AnthropicRateLimitException` and the 5xx exception with a short retry, and let everything else surface to the caller, which is responsible for degrading gracefully.

Use `client.Messages.Stream` for the chatbot (CS-705) and anywhere `MaxTokens` is large; a non-streaming call at high `max_tokens` risks an HTTP timeout.

### 2 — Summary generation with staleness detection

**File:** `backend/src/CustomerSupport.Application/Ai/Commands/GenerateTicketSummaryCommand.cs`

Build the prompt from the ticket and its non-internal messages — an internal note must not leak into a summary an agent may paste to a customer.

Hash the source so staleness is detectable rather than guessed:

```csharp
var source = string.Join("\n", messages.Select(m => $"{m.AuthorType}: {m.BodyText}"));
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

// Reuse a fresh suggestion rather than paying to regenerate the same thing.
var existing = await db.AiSuggestions
    .Where(s => s.TicketId == id && s.Type == AiSuggestionType.TicketSummary)
    .OrderByDescending(s => s.CreatedAt)
    .FirstOrDefaultAsync(ct);

if (existing is not null && existing.SourceHash == hash && !request.Force)
{
    return Map(existing, isStale: false);
}
```

The system prompt states the output language explicitly (`ticket.Language`), asks for three short sections — the issue, what has been tried, current state — and forbids inventing facts not present in the conversation.

Wrap the call so failure is non-fatal:

```csharp
try { result = await ai.CompleteAsync("ticket_summary", prompt, ct: ct); }
catch (Exception ex)
{
    // AI is an enhancement. The ticket screen must keep working.
    logger.LogWarning(ex, "Summary generation failed for ticket {TicketId}", id);
    return null;
}
```

### 3 — Review flow and spend limits

`AcceptAiSuggestionCommand` sets `Status` to `Accepted` or `Edited` (storing `EditedContent` when the text differs), stamps `ReviewedById` and `ReviewedAt`, and for summaries writes the accepted text to `Ticket.AiSummary`.

`RejectAiSuggestionCommand` sets `Rejected` with the optional reason.

Daily limit enforcement counts today's suggestions for the feature and branch:

```csharp
if (config.DailyRequestLimit > 0)
{
    var today = clock.UtcNow.Date;
    var used = await db.AiSuggestions.CountAsync(s =>
        s.Type == feature && s.BranchId == config.BranchId && s.CreatedAt >= today, ct);

    if (used >= config.DailyRequestLimit)
        throw new ConflictException(
            $"The daily AI limit for this feature ({config.DailyRequestLimit}) has been reached.");
}
```

## Frontend Tasks

### 4 — Summary panel

**File:** `frontend/src/app/features/agent/tickets/panels/ai-summary.component.ts`

A collapsible panel above the conversation, rendered only when AI is enabled and the agent holds `ai.suggestions.use` — a disabled feature shows nothing rather than a dead button.

States: not generated (a **Summarise** button), generating (a skeleton with a cancel option), ready (the summary with **Accept**, **Edit** and **Dismiss**), and stale (an amber banner: "New messages have arrived since this summary was written" with **Regenerate**).

Show model and token counts in small print behind a details toggle — useful when someone asks what this costs, invisible the rest of the time.

Editing opens an inline textarea; saving sends the edited text with the accept.

### 5 — AI configuration screen

**File:** `frontend/src/app/features/admin/ai/ai-config.page.ts`

One card per feature: enabled toggle, model select (from a curated list of current ids — never a free-text field, since an invented id fails at call time), effort select, max tokens, minimum confidence, run-automatically toggle and daily limit.

A system prompt textarea per feature with a note that changes take effect on the next generation.

A global banner reflecting the `ai.enabled` master switch, with a link to the setting.

## Verification Steps

1. `dotnet build` succeeds with the Anthropic package added.
2. With AI disabled, confirm no AI controls render and the endpoint refuses.
3. Enable summaries, open a ticket with 10 messages, and generate: a coherent summary appears in the ticket language.
4. Confirm the suggestion row records model, prompt and completion tokens, latency and the source hash.
5. Request again without changes: the cached suggestion is returned with no second API call.
6. Add a message, then reopen: the summary is marked stale and offers regeneration.
7. Confirm an internal note is not reflected in the summary.
8. Accept a summary: it is written to `Ticket.AiSummary` and the status is Accepted.
9. Edit then accept: both versions are stored and the status is Edited.
10. Reject with a reason: it is recorded.
11. Set the daily limit to 1 and generate twice: the second is refused with a clear message.
12. Point the client at an invalid key and generate: the ticket screen still works and the failure is logged.
13. Confirm no request sends `temperature` or `budget_tokens` — both would return a 400.

## Done Criteria

- [ ] `IAiCompletionService` is implemented with the official Anthropic C# SDK.
- [ ] Requests use adaptive thinking and an effort level, and send no sampling parameters.
- [ ] Model ids are exact, with no date suffix, and chosen from a curated list in the UI.
- [ ] Every generation records provenance, tokens, latency and a source hash.
- [ ] Staleness detection works and regeneration is offered rather than assumed.
- [ ] Accept, edit and reject are recorded with both versions stored.
- [ ] Daily limits are enforced per feature and branch.
- [ ] Every failure path leaves the ticket screen fully usable.
- [ ] The master switch hides AI controls entirely when off.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
