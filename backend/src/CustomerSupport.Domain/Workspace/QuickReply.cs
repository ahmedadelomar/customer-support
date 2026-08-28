using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Workspace;

/// <summary>
/// A reusable canned response (Agent Dashboard / Quick replies). Bodies are bilingual and support
/// placeholder tokens such as <c>{{customer.displayName}}</c> resolved at insertion time.
/// </summary>
public class QuickReply : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    /// <summary>Personal, Team or Global. Determines who can see and use the reply.</summary>
    public string Scope { get; set; } = "Personal";
    /// <summary>Owner for personal scope.</summary>
    public Guid? OwnerId { get; set; }
    /// <summary>Owning team for team scope.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Typed shortcut that expands the reply in the composer, for example <c>/refund</c>.</summary>
    public string? Shortcut { get; set; }
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Body { get; set; } = new();

    public Guid? CategoryId { get; set; }
    /// <summary>Restricts the reply to one channel when the wording is channel-specific.</summary>
    public Guid? ChannelId { get; set; }
    /// <summary>Incremented on each insertion, used to sort the most-used replies first.</summary>
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
