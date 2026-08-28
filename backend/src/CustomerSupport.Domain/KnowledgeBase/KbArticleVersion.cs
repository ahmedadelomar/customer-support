using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.KnowledgeBase;

/// <summary>
/// Immutable snapshot of an article taken on each publish, so authors can diff and roll back.
/// </summary>
public class KbArticleVersion : BaseEntity
{
    public Guid ArticleId { get; set; }
    public KbArticle Article { get; set; } = null!;

    public int Version { get; set; }
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Summary { get; set; } = new();
    public LocalizedText Body { get; set; } = new();

    /// <summary>Author-supplied note describing what changed in this revision.</summary>
    public string? ChangeNote { get; set; }
    public Guid? EditedById { get; set; }
    public DateTimeOffset EditedAt { get; set; }
}
