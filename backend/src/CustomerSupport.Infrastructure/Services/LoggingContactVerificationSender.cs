using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Customers;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Stands in for the real email/SMS senders (CS-301, CS-304) so contact verification works today.
/// Logs the code rather than delivering it — replace the DI registration once those stories land,
/// and the API stops exposing the code automatically because <see cref="IsPlaceholder"/> goes away.
/// </summary>
public class LoggingContactVerificationSender(ILogger<LoggingContactVerificationSender> logger)
    : IContactVerificationSender
{
    public bool IsPlaceholder => true;

    public Task SendAsync(CustomerContact contact, string code, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Verification code for contact {ContactId} ({Type} {Value}): {Code} — " +
            "no real channel is wired up yet (CS-301/CS-304), so this is logged only.",
            contact.Id, contact.Type, contact.Value, code);

        return Task.CompletedTask;
    }
}
