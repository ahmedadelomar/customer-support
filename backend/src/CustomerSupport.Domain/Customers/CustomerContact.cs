using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Customers;

/// <summary>
/// One reachable address for a customer (Customer Management / Contact details).
/// Exactly one row per <see cref="ContactType"/> may be <see cref="IsPrimary"/>.
/// </summary>
public class CustomerContact : BaseEntity, IAuditable, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public ContactType Type { get; set; }
    /// <summary>Raw value as entered.</summary>
    public string Value { get; set; } = string.Empty;
    /// <summary>Lower-cased email or E.164 phone, used for dedupe and inbound-message matching.</summary>
    public string NormalizedValue { get; set; } = string.Empty;
    /// <summary>Free-text label such as <c>Work</c> / <c>Home</c>.</summary>
    public string? Label { get; set; }

    public bool IsPrimary { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    /// <summary>False when the customer opted out of marketing/notifications on this address.</summary>
    public bool AllowNotifications { get; set; } = true;

    public string? CountryCode { get; set; }
    public string? City { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
