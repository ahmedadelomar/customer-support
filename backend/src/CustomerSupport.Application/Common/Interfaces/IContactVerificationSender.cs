using CustomerSupport.Domain.Customers;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// Delivers a contact verification code through the channel matching the contact's type.
/// Until CS-301 (email) and CS-304 (SMS) land, this is a logging placeholder — see
/// <see cref="IsPlaceholder"/>.
/// </summary>
public interface IContactVerificationSender
{
    /// <summary>
    /// True for the temporary logging implementation. The verification command checks this to
    /// decide whether to hand the raw code back in the API response — a real sender never allows
    /// that, so the moment CS-301/CS-304 replace this, the code stops being exposed automatically
    /// rather than needing a separate flag to remember to flip off.
    /// </summary>
    bool IsPlaceholder { get; }

    Task SendAsync(CustomerContact contact, string code, CancellationToken cancellationToken = default);
}
