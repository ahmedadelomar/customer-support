using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Tickets;

/// <summary>Self-referencing category tree (Ticket Management / Categories and priorities).</summary>
public class TicketCategory : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid? ParentId { get; set; }
    public TicketCategory? Parent { get; set; }

    public string Code { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    /// <summary>Materialised ancestor path (<c>/billing/refunds/</c>) so subtree queries stay a single index seek.</summary>
    public string Path { get; set; } = "/";
    public int Depth { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Applied to new tickets when the agent or channel does not set one explicitly.</summary>
    public Guid? DefaultPriorityId { get; set; }
    public Guid? DefaultDepartmentId { get; set; }
    public Guid? DefaultSlaPolicyId { get; set; }
    /// <summary>Categories hidden from the portal are agent-only.</summary>
    public bool IsVisibleInPortal { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<TicketCategory> Children { get; set; } = new List<TicketCategory>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
