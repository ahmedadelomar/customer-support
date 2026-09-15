using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Tickets.Dtos;

/// <summary>One of the customer's other open tickets, shown on the panel so duplicates are obvious.</summary>
public record CustomerPanelOtherTicketDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string StatusNameEn { get; init; } = string.Empty;
    public string StatusNameAr { get; init; } = string.Empty;
    public string StatusColorHex { get; init; } = string.Empty;
}

/// <summary>A pinned customer note, trimmed to what the panel shows (no attachments — see the full profile for those).</summary>
public record CustomerPanelNoteDto
{
    public Guid Id { get; init; }
    public string Body { get; init; } = string.Empty;
    public string? AuthorNameEn { get; init; }
    public string? AuthorNameAr { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Everything the ticket screen's customer panel needs, arriving with the ticket in one response
/// (Agent Dashboard / Customer information). <see cref="CanViewFull"/> is false when the caller lacks
/// <c>customers.view</c> — every field below it is then left at its default and the panel shows only
/// the display name, per the story's own degrade-rather-than-error rule.
/// </summary>
public record CustomerPanelDto
{
    public Guid Id { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public bool CanViewFull { get; init; }

    public string Code { get; init; } = string.Empty;
    public string? Tier { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public ChannelKey PreferredChannel { get; init; }
    public bool IsBlocked { get; init; }
    public string? BlockedReason { get; init; }
    public decimal? SatisfactionScore { get; init; }
    public DateTimeOffset? LastInteractionAt { get; init; }
    public int OpenTicketCount { get; init; }

    public IReadOnlyList<CustomerContactDto> Contacts { get; init; } = Array.Empty<CustomerContactDto>();

    /// <summary>Up to 5 of the customer's other open tickets (excludes the one currently being viewed).</summary>
    public IReadOnlyList<CustomerPanelOtherTicketDto> OtherOpenTickets { get; init; } = Array.Empty<CustomerPanelOtherTicketDto>();
    public int OtherOpenTicketCount { get; init; }

    public IReadOnlyList<CustomerPanelNoteDto> PinnedNotes { get; init; } = Array.Empty<CustomerPanelNoteDto>();

    /// <summary>Whether the caller may use the inline-patch endpoint — mirrors <c>customers.update</c>.</summary>
    public bool CanEdit { get; init; }
}
