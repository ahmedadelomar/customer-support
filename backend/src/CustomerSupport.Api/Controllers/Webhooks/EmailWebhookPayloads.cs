namespace CustomerSupport.Api.Controllers.Webhooks;

/// <summary>
/// The generic shape this pipeline expects for an inbound email. No specific real provider
/// (SendGrid inbound parse, Postmark, Mailgun, …) is wired up in this environment — adapting one
/// means mapping its actual webhook payload into this shape here, which is the one place that would
/// need to change.
/// </summary>
public record InboundEmailAttachmentPayload(string FileName, string ContentType, string ContentBase64);

public record InboundEmailWebhookPayload(
    string MessageId,
    string? InReplyTo,
    List<string>? References,
    string From,
    string? FromName,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    DateTimeOffset? SentAt,
    Dictionary<string, string>? Headers,
    List<InboundEmailAttachmentPayload>? Attachments);

/// <summary>A delivery/bounce/complaint callback, correlated to the outbound send by <c>ProviderMessageId</c>.</summary>
public record EmailDeliveryStatusPayload(string ProviderMessageId, string Status, string? ErrorCode, string? ErrorMessage);
