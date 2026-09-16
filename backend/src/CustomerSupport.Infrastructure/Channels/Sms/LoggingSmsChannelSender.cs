using CustomerSupport.Application.Channels.Outbound;
using CustomerSupport.Application.Channels.Sms;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Channels.Sms;

/// <summary>
/// Stands in for a real SMS gateway so outbound replies work end to end today. Logs rather than
/// delivering — replace the DI registration once a real provider is wired up. Same placeholder
/// convention as <see cref="Channels.Email.LoggingEmailChannelSender"/>.
/// </summary>
public class LoggingSmsChannelSender(ILogger<LoggingSmsChannelSender> logger) : ISmsChannelSender
{
    public Task<ChannelSendResult> SendAsync(string toPhone, string fromIdentifier, string body, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[SMS] {From} -> {To}: {Body} (no real SMS provider is wired up yet, CS-304 — logged only)",
            fromIdentifier, toPhone, body);

        return Task.FromResult(new ChannelSendResult(true, ProviderMessageId: Guid.NewGuid().ToString("N"), null, null));
    }
}
