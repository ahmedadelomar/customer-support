using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Ai;

/// <summary>
/// One AI output offered to an agent. Every AI feature except the chatbot writes rows here, which
/// keeps the human-in-the-loop review flow, cost tracking and accept-rate reporting in one place.
/// Suggestions are advisory: nothing is applied to a ticket until an agent accepts it.
/// </summary>
public class AiSuggestion : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid TicketId { get; set; }

    public AiSuggestionType Type { get; set; }
    public AiSuggestionStatus Status { get; set; } = AiSuggestionStatus.Pending;

    /// <summary>Model output. Plain text for summaries and replies, JSON for categorisation results.</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>What the agent actually sent, when they edited the suggestion before using it.</summary>
    public string? EditedContent { get; set; }
    /// <summary>Model self-reported confidence, 0 to 1. Below the configured floor the UI hides the suggestion.</summary>
    public decimal? Confidence { get; set; }

    /// <summary>For Categorization: the proposed category. For SuggestedSolution: the proposed article.</summary>
    public Guid? SuggestedCategoryId { get; set; }
    public Guid? SuggestedArticleId { get; set; }
    public Guid? SuggestedPriorityId { get; set; }

    // --- Provenance and cost ---
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    /// <summary>Version of the prompt template used, so quality regressions can be traced.</summary>
    public string? PromptVersion { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int LatencyMs { get; set; }
    /// <summary>Hash of the ticket content the suggestion was generated from, used to detect staleness.</summary>
    public string? SourceHash { get; set; }

    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    /// <summary>Free-text reason captured when an agent rejects a suggestion. Feeds prompt tuning.</summary>
    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
