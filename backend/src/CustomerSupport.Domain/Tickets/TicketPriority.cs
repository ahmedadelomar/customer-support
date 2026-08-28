using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Tickets;

/// <summary>Configurable priority. SLA targets are defined per priority, so this is a table, not an enum.</summary>
public class TicketPriority : BaseEntity, IAuditable
{
    public string Code { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    /// <summary>Sort weight — higher is more urgent. Escalation "raise priority" moves one level up.</summary>
    public int Level { get; set; }
    public string ColorHex { get; set; } = "#64748B";
    public string? Icon { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
