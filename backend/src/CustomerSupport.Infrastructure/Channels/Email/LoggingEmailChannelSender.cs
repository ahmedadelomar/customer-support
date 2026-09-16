using CustomerSupport.Application.Channels.Outbound;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Channels.Email;

/// <summary>
/// Stands in for a real SMTP/provider connection so outbound replies work end to end today. Logs
/// rather than delivering — replace the DI registration once a real mailbox is wired up; the outbox
/// handler's retry, backoff and delivery-log logic does not change. Same placeholder convention as
/// <see cref="Services.LoggingExternalNotificationSender"/> and <see cref="Services.LoggingContactVerificationSender"/>.
/// </summary>
public class LoggingEmailChannelSender(ILogger<LoggingEmailChannelSender> logger) : IEmailChannelSender
{
    public Task<ChannelSendResult> SendAsync(
        string toAddress, string fromAddress, string subject, string bodyHtml, string bodyText,
        string messageId, string? inReplyTo, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[Email] {From} -> {To}: {Subject} (Message-ID {MessageId}{InReplyTo}) — {Body} " +
            "(no real mail provider is wired up yet, CS-301 — logged only)",
            fromAddress, toAddress, subject, messageId,
            inReplyTo is null ? "" : $", In-Reply-To {inReplyTo}", bodyText);

        return Task.FromResult(new ChannelSendResult(true, ProviderMessageId: messageId, null, null));
    }
}
