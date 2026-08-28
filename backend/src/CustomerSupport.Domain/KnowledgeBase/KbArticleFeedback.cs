using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.KnowledgeBase;

/// <summary>
/// A helpful or not-helpful vote, optionally with a comment. Feeds the content-gap report that tells
/// authors which articles to rewrite.
/// </summary>
public class KbArticleFeedback : BaseEntity
{
    public Guid ArticleId { get; set; }
    public KbArticle Article { get; set; } = null!;

    /// <summary>Null for anonymous portal visitors.</summary>
    public Guid? CustomerId { get; set; }
    public Guid? UserId { get; set; }
    /// <summary>Prevents repeat voting by the same anonymous visitor.</summary>
    public string? VisitorKey { get; set; }

    public bool IsHelpful { get; set; }
    public string? Comment { get; set; }
    public string Language { get; set; } = "ar";
    public DateTimeOffset SubmittedAt { get; set; }
}
