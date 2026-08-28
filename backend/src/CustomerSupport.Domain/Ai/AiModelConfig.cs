using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Ai;

/// <summary>
/// Per-feature model configuration, editable by administrators without a deployment. One active row
/// per <see cref="Feature"/> per branch.
/// </summary>
public class AiModelConfig : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    /// <summary>Which capability this configuration drives. Null Feature means the chatbot.</summary>
    public AiSuggestionType? Feature { get; set; }
    /// <summary>Set instead of Feature for the conversational assistant.</summary>
    public bool IsChatbot { get; set; }

    public string Provider { get; set; } = "anthropic";

    /// <summary>Exact model id, with no date suffix. See the Claude API model table.</summary>
    public string ModelId { get; set; } = "claude-opus-5";

    /// <summary>
    /// Reasoning depth: low, medium, high, xhigh or max. This replaces temperature — the
    /// sampling parameters are rejected by the current Claude models, which use adaptive
    /// thinking and an effort level instead.
    /// </summary>
    public string Effort { get; set; } = "medium";

    public int MaxTokens { get; set; } = 4096;

    /// <summary>System prompt template. Supports bilingual output instructions and placeholder tokens.</summary>
    public string? SystemPrompt { get; set; }
    /// <summary>Suggestions below this confidence are never shown to agents.</summary>
    public decimal MinConfidence { get; set; } = 0.5m;

    /// <summary>Master switch so a misbehaving feature can be turned off instantly.</summary>
    public bool IsEnabled { get; set; } = true;
    /// <summary>Generate automatically on ticket create or reply, rather than only on agent request.</summary>
    public bool RunAutomatically { get; set; }
    /// <summary>Guard rail on spend: maximum generations per day for this feature. Zero means unlimited.</summary>
    public int DailyRequestLimit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
