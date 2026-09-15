namespace CustomerSupport.Application.Tickets.Priorities;

/// <summary>Priority row for the admin scale editor, with usage counts so deleting/deactivating is an informed choice.</summary>
public record TicketPriorityAdminDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public int Level { get; init; }
    public string ColorHex { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public int TicketCount { get; init; }
    public int SlaTargetCount { get; init; }
}
