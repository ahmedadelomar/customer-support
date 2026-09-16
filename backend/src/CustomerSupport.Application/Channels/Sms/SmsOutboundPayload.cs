using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Application.Channels.Sms;

/// <summary>Business data captured at queue time — who, what and the plain body. SMS carries no attachments and no separate presentation step (no HTML), unlike email.</summary>
public record SmsOutboundPayload(
    Guid TicketMessageId,
    Guid TicketId,
    Guid? ChannelAccountId,
    Guid? BranchId,
    string ToPhone,
    string FromIdentifier,
    string Body);

/// <summary>Writes the `channel.sms.outbound` outbox row every SMS reply queues through — same "the caller saves" convention as <c>EmailChannelOutbox</c>.</summary>
public static class SmsChannelOutbox
{
    public const string OutboxType = "channel.sms.outbound";

    public static void Queue(IAppDbContext db, IDateTimeProvider clock, SmsOutboundPayload payload)
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
}
