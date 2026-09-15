using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Customers;

/// <summary>
/// One verification attempt for a <see cref="CustomerContact"/>. A new row is created every time a
/// code is sent; the previous unconfirmed row for the same contact is expired immediately, so at
/// most one code is ever active.
/// </summary>
public class ContactVerification : BaseEntity
{
    public Guid CustomerContactId { get; set; }
    public CustomerContact CustomerContact { get; set; } = null!;

    /// <summary>SHA-256 of the 6-digit code. Storing it in clear would make the log a credential store.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Wrong-code guesses against this row. Confirmation is refused once this reaches 5.</summary>
    public int Attempts { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
