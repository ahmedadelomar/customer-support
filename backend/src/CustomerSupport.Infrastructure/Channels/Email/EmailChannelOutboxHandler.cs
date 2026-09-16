using System.Net;
using System.Text.Json;
using CustomerSupport.Application.Channels.Outbound;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Channels.Email;

/// <summary>
/// Delivers <c>channel.email.outbound</c> outbox rows — every agent reply and mailbox
/// auto-acknowledgement queued by CS-301. Renders the branded HTML wrapper at send time (never at
/// queue time, so a branding change between the two is never stale), then writes the
/// <see cref="MessageDeliveryLog"/> the agent-facing delivery indicator reads.
/// </summary>
public class EmailChannelOutboxHandler(
    AppDbContext db, IEmailTemplateRenderer renderer, IEmailChannelSender sender, IDateTimeProvider clock)
    : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool CanHandle(string type) => type == EmailChannelOutbox.OutboxType;

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<EmailOutboundPayload>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Malformed email outbox payload.");

        var account = payload.ChannelAccountId is { } accountId
            ? await db.ChannelAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
            : null;

        var signatureText = account?.Signature.For(payload.Language);
        var signatureHtml = string.IsNullOrWhiteSpace(signatureText)
            ? ""
            : WebUtility.HtmlEncode(signatureText).Replace("\n", "<br/>");

        var bodyHtml = payload.BodyHtml ?? WebUtility.HtmlEncode(payload.BodyText).Replace("\n", "<br/>");
        var renderedHtml = await renderer.RenderAsync(bodyHtml, signatureHtml, payload.Language, payload.BranchId, ct);

        var deliveryLog = new MessageDeliveryLog
        {
            TicketMessageId = payload.TicketMessageId,
            ChannelAccountId = payload.ChannelAccountId,
            Recipient = payload.ToAddress,
            ProviderName = "logging-placeholder",
            Attempt = message.Attempts + 1,
            UpdatedAt = clock.UtcNow,
        };

        var result = await sender.SendAsync(
            payload.ToAddress, payload.FromAddress, payload.Subject, renderedHtml, payload.BodyText,
            payload.MessageId, payload.InReplyTo, ct);

        if (result.Success)
        {
            deliveryLog.Status = MessageDeliveryStatus.Sent;
            deliveryLog.ProviderMessageId = result.ProviderMessageId ?? payload.MessageId;
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
            throw new InvalidOperationException(result.ErrorMessage ?? "Email send failed.");
        }
    }
}
