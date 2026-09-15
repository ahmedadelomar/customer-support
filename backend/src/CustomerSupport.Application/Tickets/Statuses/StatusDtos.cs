using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Tickets.Statuses;

/// <summary>Status row for the admin editor, with a usage count so a `Kind` change or deletion is an informed choice.</summary>
public record TicketStatusAdminDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public TicketStatusKind Kind { get; init; }
    public string ColorHex { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsTerminal { get; init; }
    public bool PausesSla { get; init; }
    public bool IsDefault { get; init; }
    public bool IsVisibleInPortal { get; init; }
    public bool IsActive { get; init; }
    public int TicketCount { get; init; }
}
