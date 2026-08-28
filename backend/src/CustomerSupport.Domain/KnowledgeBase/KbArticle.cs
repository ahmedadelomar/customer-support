using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.KnowledgeBase;

/// <summary>
/// A single piece of knowledge-base content. One entity serves FAQs, help articles, solutions and
/// guides, distinguished by <see cref="Type"/>, because they share authoring, search and feedback.
/// </summary>
public class KbArticle : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid CategoryId { get; set; }
    public KbCategory Category { get; set; } = null!;

    public string Slug { get; set; } = string.Empty;
    public KbArticleType Type { get; set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.Draft;

    public LocalizedText Title { get; set; } = new();
    /// <summary>Short teaser used in search results and article cards.</summary>
    public LocalizedText Summary { get; set; } = new();
    /// <summary>Sanitised HTML body. Stored per language so either can be published independently.</summary>
    public LocalizedText Body { get; set; } = new();

    /// <summary>Comma-separated keywords boosting full-text relevance beyond the body text.</summary>
    public string? Keywords { get; set; }
    /// <summary>Categories this article resolves, letting the AI suggest it for matching tickets.</summary>
    public string? RelatedCategoryIds { get; set; }

    /// <summary>Public articles appear in the portal; internal ones are agent-only runbooks.</summary>
    public bool IsPublic { get; set; } = true;
    /// <summary>Pins the article to the top of its category and the portal home.</summary>
    public bool IsFeatured { get; set; }

    public Guid? AuthorId { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    /// <summary>Content past this date is flagged for review in the authoring dashboard.</summary>
    public DateTimeOffset? ReviewDueAt { get; set; }

    /// <summary>Current version number, incremented on every published edit.</summary>
    public int Version { get; set; } = 1;

    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    /// <summary>How many times an agent inserted this article into a reply. Measures real usefulness.</summary>
    public int UsedInReplyCount { get; set; }
    public int AttachmentCount { get; set; }

    public ICollection<KbArticleVersion> Versions { get; set; } = new List<KbArticleVersion>();
    public ICollection<KbArticleFeedback> Feedback { get; set; } = new List<KbArticleFeedback>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
