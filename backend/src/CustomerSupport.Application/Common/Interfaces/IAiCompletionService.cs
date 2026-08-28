namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>Result of one model call, including the cost and latency the AI reports need.</summary>
public record AiCompletionResult(
    string Content,
    string Provider,
    string ModelId,
    int PromptTokens,
    int CompletionTokens,
    int LatencyMs,
    decimal? Confidence);

/// <summary>
/// Thin provider-agnostic wrapper over the LLM. Every AI feature goes through this so the model,
/// prompts and spend limits stay configurable from <c>AiModelConfig</c> rather than hard-coded.
/// </summary>
public interface IAiCompletionService
{
    /// <summary>Runs a single completion using the configuration registered for <paramref name="featureKey"/>.</summary>
    Task<AiCompletionResult> CompleteAsync(
        string featureKey,
        string userPrompt,
        string? systemPromptOverride = null,
        CancellationToken ct = default);
}
