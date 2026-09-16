namespace CustomerSupport.Application.Channels.Outbound;

/// <summary>The result of actually handing a message to the provider.</summary>
public record ChannelSendResult(bool Success, string? ProviderMessageId, string? ErrorCode, string? ErrorMessage);

/// <summary>
/// Delivers one outbound customer-facing email through whichever provider a mailbox
/// (<c>ChannelAccount</c>) is configured with (Communication Channels / Email channel). Until a real
/// provider is wired up, the registered implementation is a logging placeholder — the outbox handler
/// is built against this interface so swapping in SMTP/SendGrid/etc. later is a DI registration
/// change, not a call-site change. Same shape as <c>IExternalNotificationSender</c> (CS-504), kept
/// separate because that one speaks to staff about internal events and this one speaks to customers
/// about their own tickets — different senders, different provider accounts, different content rules.
/// </summary>
public interface IEmailChannelSender
{
    Task<ChannelSendResult> SendAsync(
        string toAddress, string fromAddress, string subject, string bodyHtml, string bodyText,
        string messageId, string? inReplyTo, CancellationToken ct = default);
}

/// <summary>
/// Wraps a reply's plain content in the account's bilingual signature and the tenant's branding
/// (Platform / Custom branding) — colours and product name only; no external image fetch, so a
/// broken logo URL can never break outbound mail.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(
        string bodyHtml, string signatureHtml, string language, Guid? branchId, CancellationToken ct = default);
}
