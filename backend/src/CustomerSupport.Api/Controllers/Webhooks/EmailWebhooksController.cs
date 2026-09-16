using System.Text.Json;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Channels.Inbound;
using CustomerSupport.Application.Common;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Api.Controllers.Webhooks;

/// <summary>
/// Provider-facing inbound endpoints for the email channel (Communication Channels / Email
/// channel). Anonymous by necessity — the caller is a mail provider, not a signed-in user — and
/// every call is HMAC-verified instead, refusing outright when the account has no secret configured
/// rather than silently skipping verification.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/email")]
public class EmailWebhooksController(
    IInboundMessagePipeline pipeline,
    IChannelWebhookSecrets secrets,
    IAppDbContext db,
    INotificationDispatcher notifications,
    IDateTimeProvider clock,
    ILogger<EmailWebhooksController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Provider inbound webhook: a new or replied-to email arrived.</summary>
    [HttpPost("{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Inbound(Guid accountId, CancellationToken ct)
    {
        var raw = await ReadRawBodyAsync(ct);
        if (!await VerifySignatureAsync(accountId, raw, ct))
        {
            return Unauthorized();
        }

        InboundEmailWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InboundEmailWebhookPayload>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Malformed JSON payload." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.MessageId) || string.IsNullOrWhiteSpace(payload.From))
        {
            return BadRequest(new { message = "messageId and from are required." });
        }

        IReadOnlyList<InboundAttachment> attachments;
        try
        {
            attachments = (payload.Attachments ?? [])
                .Select(a => new InboundAttachment(a.FileName, a.ContentType, Convert.FromBase64String(a.ContentBase64)))
                .ToList();
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "An attachment's contentBase64 is not valid base64." });
        }

        var message = new InboundMessage(
            ChannelKey.Email,
            accountId,
            payload.MessageId,
            payload.InReplyTo,
            payload.References ?? [],
            payload.From,
            payload.FromName,
            payload.Subject,
            payload.TextBody ?? string.Empty,
            payload.HtmlBody,
            payload.SentAt ?? clock.UtcNow,
            attachments,
            payload.Headers ?? new Dictionary<string, string>());

        var ticketId = await pipeline.IngestAsync(message, ct);
        return Ok(new { ticketId, duplicate = ticketId is null });
    }

    /// <summary>Delivery, bounce and complaint callbacks for a previously sent reply.</summary>
    [HttpPost("{accountId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(Guid accountId, CancellationToken ct)
    {
        var raw = await ReadRawBodyAsync(ct);
        if (!await VerifySignatureAsync(accountId, raw, ct))
        {
            return Unauthorized();
        }

        EmailDeliveryStatusPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<EmailDeliveryStatusPayload>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Malformed JSON payload." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ProviderMessageId))
        {
            return BadRequest(new { message = "providerMessageId is required." });
        }

        var log = await db.MessageDeliveryLogs
            .FirstOrDefaultAsync(l => l.ProviderMessageId == payload.ProviderMessageId, ct);

        if (log is null)
        {
            return NotFound();
        }

        switch (payload.Status.ToLowerInvariant())
        {
            case "delivered":
                log.Status = MessageDeliveryStatus.Delivered;
                log.DeliveredAt = clock.UtcNow;
                break;
            case "opened":
                log.Status = MessageDeliveryStatus.Read;
                log.ReadAt = clock.UtcNow;
                break;
            case "bounced":
                log.Status = MessageDeliveryStatus.Bounced;
                log.ErrorCode = payload.ErrorCode;
                log.ErrorMessage = payload.ErrorMessage;
                await HandleHardBounceAsync(log, ct);
                break;
            default:
                logger.LogWarning("Unrecognised email delivery status {Status} for {ProviderMessageId}.", payload.Status, payload.ProviderMessageId);
                break;
        }

        log.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>A silently bouncing address is how tickets go unanswered — mark it unverified and tell the agent.</summary>
    private async Task HandleHardBounceAsync(Domain.Channels.MessageDeliveryLog log, CancellationToken ct)
    {
        var normalized = ContactNormalizer.Normalize(log.Recipient);
        var contact = await db.CustomerContacts.FirstOrDefaultAsync(c => c.NormalizedValue == normalized, ct);
        if (contact is not null)
        {
            contact.IsVerified = false;
        }

        var ticketMessage = await db.TicketMessages.FirstOrDefaultAsync(m => m.Id == log.TicketMessageId, ct);
        if (ticketMessage is null)
        {
            return;
        }

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketMessage.TicketId, ct);
        if (ticket?.AssignedAgentId is not { } agentId)
        {
            return;
        }

        await notifications.DispatchAsync(
            agentId, "ticket.emailBounced",
            $"Email bounced: {ticket.Number}", $"ارتد البريد الإلكتروني: {ticket.Number}",
            $"Your reply on ticket {ticket.Number} to {log.Recipient} bounced.{(log.ErrorMessage is null ? "" : $" ({log.ErrorMessage})")}",
            $"ارتد ردك على التذكرة {ticket.Number} إلى {log.Recipient}.",
            link: $"/agent/tickets/{ticket.Id}", severity: "Warning", ct: ct);
    }

    private async Task<bool> VerifySignatureAsync(Guid accountId, string rawBody, CancellationToken ct)
    {
        var secret = await secrets.GetSecretAsync(accountId, ct);
        if (secret is null)
        {
            logger.LogWarning("Webhook call for channel account {AccountId} refused: no secret configured.", accountId);
            return false;
        }

        var signature = Request.Headers[WebhookSignature.HeaderName].FirstOrDefault();
        return WebhookSignature.Verify(rawBody, signature, secret);
    }

    private async Task<string> ReadRawBodyAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        return await reader.ReadToEndAsync(ct);
    }
}
