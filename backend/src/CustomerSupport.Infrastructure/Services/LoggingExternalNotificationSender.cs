using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Stands in for the real email/SMS/push providers (CS-301, CS-302, CS-304) so the outbox dispatcher
/// (CS-504) has somewhere to deliver to today. Logs rather than sending — replace the DI registration
/// once those stories land; the outbox job's retry, backoff and abandonment logic does not change.
/// </summary>
public class LoggingExternalNotificationSender(ILogger<LoggingExternalNotificationSender> logger)
    : IExternalNotificationSender
{
    public Task SendAsync(
        NotificationChannel channel, string? recipientEmail, string? recipientPhone,
        string title, string body, string? link, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[{Channel}] to {Recipient}: {Title} — {Body}{Link} (no real {Channel} provider is wired up yet, CS-301/302/304 — logged only)",
            channel, recipientEmail ?? recipientPhone ?? "(unknown)", title, body,
            link is null ? "" : $" [{link}]", channel);

        return Task.CompletedTask;
    }
}
