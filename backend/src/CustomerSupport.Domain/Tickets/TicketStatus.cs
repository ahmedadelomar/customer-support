using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Tickets;

/// <summary>
/// A configurable workflow status. <see cref="Kind"/> keeps behaviour (SLA pause, closure,
/// reporting buckets) predictable no matter how administrators rename or add statuses.
/// </summary>
public class TicketStatus : BaseEntity, IAuditable
{
    public string Code { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public TicketStatusKind Kind { get; set; }
    public string ColorHex { get; set; } = "#64748B";
    public int DisplayOrder { get; set; }

    /// <summary>Terminal statuses stop every SLA clock and make the ticket read-only for the customer.</summary>
    public bool IsTerminal { get; set; }
    /// <summary>Pending/on-hold statuses pause the resolution clock while waiting on the customer.</summary>
    public bool PausesSla { get; set; }
    /// <summary>Exactly one row is the status applied to newly created tickets.</summary>
    public bool IsDefault { get; set; }
    public bool IsVisibleInPortal { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
