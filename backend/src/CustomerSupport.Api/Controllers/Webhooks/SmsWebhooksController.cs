using System.Text.Json;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Channels.Inbound;
using CustomerSupport.Application.Channels.Sms;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Channels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Api.Controllers.Webhooks;

/// <summary>
/// Provider-facing inbound endpoints for the SMS channel (Communication Channels / SMS channel,
/// CS-304). Anonymous by necessity and HMAC-verified like the email webhooks, with one addition
/// email does not need: STOP/START keywords are intercepted before the shared pipeline ever runs,
/// since an opt-out message must never become a ticket.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/sms")]
public class SmsWebhooksController(
    IInboundMessagePipeline pipeline,
    IChannelWebhookSecrets secrets,
    ISender mediator,
    IAppDbContext db,
    IDateTimeProvider clock,
    ILogger<SmsWebhooksController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] OptOutKeywords = ["STOP", "UNSUBSCRIBE", "CANCEL", "END", "QUIT", "إلغاء", "توقف", "الغاء"];
    private static readonly string[] OptInKeywords = ["START", "بدء", "اشتراك"];

    /// <summary>Provider inbound webhook: an SMS arrived. STOP/START are handled here and never reach the pipeline.</summary>
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

        InboundSmsWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InboundSmsWebhookPayload>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Malformed JSON payload." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.MessageId) || string.IsNullOrWhiteSpace(payload.From))
        {
            return BadRequest(new { message = "messageId and from are required." });
        }

        var keyword = payload.Body.Trim().ToUpperInvariant();
        if (OptOutKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
        {
            await mediator.Send(new ApplySmsOptCommand(accountId, payload.From, AllowNotifications: false), ct);
            return Ok(new { optOut = true });
        }

        if (OptInKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
        {
            await mediator.Send(new ApplySmsOptCommand(accountId, payload.From, AllowNotifications: true), ct);
            return Ok(new { optIn = true });
        }

        var message = new InboundMessage(
            ChannelKey.Sms,
            accountId,
            payload.MessageId,
            null,
            [],
            payload.From,
            payload.FromName,
            null,
            payload.Body,
            null,
            payload.SentAt ?? clock.UtcNow,
            [],
            new Dictionary<string, string>());

        var ticketId = await pipeline.IngestAsync(message, ct);
        return Ok(new { ticketId, duplicate = ticketId is null });
    }

    /// <summary>Delivery status callbacks for a previously sent reply.</summary>
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

        SmsDeliveryStatusPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SmsDeliveryStatusPayload>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Malformed JSON payload." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ProviderMessageId))
        {
            return BadRequest(new { message = "providerMessageId is required." });
        }

        var log = await db.MessageDeliveryLogs.FirstOrDefaultAsync(l => l.ProviderMessageId == payload.ProviderMessageId, ct);
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
            case "failed":
            case "undelivered":
                log.Status = MessageDeliveryStatus.Failed;
                log.ErrorCode = payload.ErrorCode;
                log.ErrorMessage = payload.ErrorMessage;
                break;
            default:
                logger.LogWarning("Unrecognised SMS delivery status {Status} for {ProviderMessageId}.", payload.Status, payload.ProviderMessageId);
                break;
        }

        log.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
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
