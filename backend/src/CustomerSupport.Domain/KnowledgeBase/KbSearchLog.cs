using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.KnowledgeBase;

/// <summary>
/// Every knowledge-base query (Knowledge Base / Search). Zero-result queries are the primary input to
/// the content-gap report, which is why this is persisted rather than only logged.
/// </summary>
public class KbSearchLog : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public string Query { get; set; } = string.Empty;
    /// <summary>Normalised query used for grouping, with diacritics and casing stripped.</summary>
    public string NormalizedQuery { get; set; } = string.Empty;
    public string Language { get; set; } = "ar";

    public int ResultCount { get; set; }
    /// <summary>Article the searcher opened from the results, when any. Null means no result was useful.</summary>
    public Guid? ClickedArticleId { get; set; }
    /// <summary>Zero-based rank of the clicked result, for relevance tuning.</summary>
    public int? ClickedRank { get; set; }

    /// <summary>Portal, Agent or Chatbot. Separates customer self-service from agent lookups.</summary>
    public string Source { get; set; } = "Portal";
    public Guid? CustomerId { get; set; }
    public Guid? UserId { get; set; }
    public int DurationMs { get; set; }
    public DateTimeOffset SearchedAt { get; set; }
}
