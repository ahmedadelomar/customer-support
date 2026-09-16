using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Application.Channels.Outbound;

/// <summary>
/// Business data captured at queue time — who, what and the plain content. Presentation (the
/// signature and the branded HTML wrapper) is applied at send time instead, by
/// <c>IEmailTemplateRenderer</c>, so a branding change between queueing and delivery is never stale.
/// </summary>
public record EmailOutboundPayload(
    Guid TicketMessageId,
    Guid TicketId,
    Guid? ChannelAccountId,
    Guid? BranchId,
    string ToAddress,
    string FromAddress,
    string Subject,
    string BodyText,
    string? BodyHtml,
    string Language,
    string MessageId,
    string? InReplyTo);

/// <summary>
/// Writes the `channel.email.outbound` outbox row every customer-facing email reply and
/// auto-acknowledgement queues through — the same "the caller saves" convention as
/// <c>ITicketEventRecorder</c>/<c>INotificationDispatcher</c>, so a rolled-back ticket mutation can
/// never leave an email queued for something that never happened.
/// </summary>
public static class EmailChannelOutbox
{
    public const string OutboxType = "channel.email.outbound";

    public static void Queue(IAppDbContext db, IDateTimeProvider clock, EmailOutboundPayload payload)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType,
            PayloadJson = JsonSerializer.Serialize(payload),
            AggregateType = nameof(TicketMessage),
            AggregateId = payload.TicketMessageId,
            OccurredAt = clock.UtcNow,
            NextAttemptAt = clock.UtcNow,
        });
    }

    /// <summary>A freshly generated RFC 5322 <c>Message-ID</c> for an outbound reply, so the customer's next reply threads back onto it.</summary>
    public static string NewMessageId(string mailboxIdentifier)
    {
        var at = mailboxIdentifier.IndexOf('@');
        var domain = at >= 0 && at < mailboxIdentifier.Length - 1 ? mailboxIdentifier[(at + 1)..] : "localhost";
        return $"<{Guid.NewGuid():N}@{domain}>";
    }
}
