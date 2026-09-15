using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Tickets;

/// <summary>A user's saved ticket list filter set (Ticket Management / Create and track tickets).</summary>
public class SavedTicketView : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid OwnerId { get; set; }
    public LocalizedText Name { get; set; } = new();

    /// <summary>The filter set as JSON, matching the list query parameters.</summary>
    public string FiltersJson { get; set; } = "{}";

    public int DisplayOrder { get; set; }

    /// <summary>Shared views are visible to the owner's whole team.</summary>
    public bool IsShared { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
