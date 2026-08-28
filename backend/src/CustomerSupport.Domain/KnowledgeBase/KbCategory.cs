using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.KnowledgeBase;

/// <summary>Nested browse tree for knowledge-base content, mirrored in the customer portal.</summary>
public class KbCategory : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid? ParentId { get; set; }
    public KbCategory? Parent { get; set; }

    public string Slug { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Materialised ancestor path so a subtree listing stays one index seek.</summary>
    public string Path { get; set; } = "/";

    /// <summary>Hidden categories are agent-only and never rendered in the portal.</summary>
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int ArticleCount { get; set; }

    public ICollection<KbCategory> Children { get; set; } = new List<KbCategory>();
    public ICollection<KbArticle> Articles { get; set; } = new List<KbArticle>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
