using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Customers.Dtos;

/// <summary>
/// Row shape for the customer list. Deliberately narrow: the grid needs these columns and nothing
/// more, so the list query never pulls the full aggregate.
/// </summary>
public record CustomerListItemDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public CustomerType Type { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? PrimaryEmail { get; init; }
    public string? PrimaryPhone { get; init; }
    public string? Tier { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public ChannelKey PreferredChannel { get; init; }
    public bool IsActive { get; init; }
    public bool IsBlocked { get; init; }
    public decimal? SatisfactionScore { get; init; }
    public DateTimeOffset? LastInteractionAt { get; init; }
    public int OpenTicketCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Projection used directly in <c>Select</c> so EF translates it to SQL.</summary>
    public static CustomerListItemDto From(Customer c, int openTickets) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Type = c.Type,
        DisplayNameEn = c.DisplayName.En,
        DisplayNameAr = c.DisplayName.Ar,
        PrimaryEmail = c.PrimaryEmail,
        PrimaryPhone = c.PrimaryPhone,
        Tier = c.Tier,
        PreferredLanguage = c.PreferredLanguage,
        PreferredChannel = c.PreferredChannel,
        IsActive = c.IsActive,
        IsBlocked = c.IsBlocked,
        SatisfactionScore = c.SatisfactionScore,
        LastInteractionAt = c.LastInteractionAt,
        OpenTicketCount = openTickets,
        CreatedAt = c.CreatedAt,
    };
}

/// <summary>A single contact row on a customer profile.</summary>
public record CustomerContactDto
{
    public Guid Id { get; init; }
    public ContactType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public string? Label { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsVerified { get; init; }
    public bool AllowNotifications { get; init; }
    public string? CountryCode { get; init; }
    public string? City { get; init; }
    public string? AddressLine { get; init; }
    public string? PostalCode { get; init; }
}

/// <summary>Full customer profile returned by the details endpoint.</summary>
public record CustomerDetailDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public CustomerType Type { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CompanyName { get; init; }
    public string? NationalIdOrCr { get; init; }
    public string? TaxNumber { get; init; }
    public string? PrimaryEmail { get; init; }
    public string? PrimaryPhone { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public ChannelKey PreferredChannel { get; init; }
    public string? TimeZoneId { get; init; }
    public string? Tier { get; init; }
    public Guid? AccountManagerId { get; init; }
    public Guid? BranchId { get; init; }
    public bool IsActive { get; init; }
    public bool IsBlocked { get; init; }
    public string? BlockedReason { get; init; }
    public decimal? SatisfactionScore { get; init; }
    public DateTimeOffset? LastInteractionAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }

    public IReadOnlyList<CustomerContactDto> Contacts { get; init; } = Array.Empty<CustomerContactDto>();

    /// <summary>Counts shown on the profile header so the UI needs no extra round trips.</summary>
    public int TotalTicketCount { get; init; }
    public int OpenTicketCount { get; init; }
    public int NoteCount { get; init; }
}
