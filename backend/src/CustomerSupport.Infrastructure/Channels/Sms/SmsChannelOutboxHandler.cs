using System.Text.Json;
using CustomerSupport.Application.Channels.Sms;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Infrastructure.Persistence;

namespace CustomerSupport.Infrastructure.Channels.Sms;

/// <summary>
/// Delivers <c>channel.sms.outbound</c> outbox rows — every agent SMS reply queued by CS-304.
/// Records the segment count and an estimated cost on the <see cref="MessageDeliveryLog"/>, which is
/// what gives the SMS cost report real data instead of an estimate computed after the fact.
/// </summary>
public class SmsChannelOutboxHandler(
    AppDbContext db, ISmsChannelSender sender, ISettingsProvider settings, IDateTimeProvider clock)
    : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool CanHandle(string type) => type == SmsChannelOutbox.OutboxType;

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<SmsOutboundPayload>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Malformed SMS outbox payload.");

        var segments = SmsSegmentCalculator.Calculate(payload.Body);
        var costPerSegment = await settings.GetAsync(SettingKeys.SmsCostPerSegment, 0.02, payload.BranchId, ct);

        var deliveryLog = new MessageDeliveryLog
        {
            TicketMessageId = payload.TicketMessageId,
            ChannelAccountId = payload.ChannelAccountId,
            Recipient = payload.ToPhone,
            ProviderName = "logging-placeholder",
            Attempt = message.Attempts + 1,
            SegmentCount = segments.SegmentCount,
            EstimatedCost = (decimal)(segments.SegmentCount * costPerSegment),
            UpdatedAt = clock.UtcNow,
        };

        var result = await sender.SendAsync(payload.ToPhone, payload.FromIdentifier, payload.Body, ct);

        if (result.Success)
        {
            deliveryLog.Status = MessageDeliveryStatus.Sent;
            deliveryLog.ProviderMessageId = result.ProviderMessageId;
            deliveryLog.SentAt = clock.UtcNow;
        }
        else
        {
            deliveryLog.Status = MessageDeliveryStatus.Failed;
            deliveryLog.ErrorCode = result.ErrorCode;
            deliveryLog.ErrorMessage = result.ErrorMessage;
        }

        db.MessageDeliveryLogs.Add(deliveryLog);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "SMS send failed.");
        }
    }
}
