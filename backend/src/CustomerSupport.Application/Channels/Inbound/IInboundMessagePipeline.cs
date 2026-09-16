using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Channels.Inbound;

public record InboundAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// One inbound message, already translated from a provider's wire format into the shape every
/// channel adapter funnels through. <see cref="ExternalMessageId"/> and
/// <see cref="ReferenceExternalIds"/> exist for email's <c>Message-ID</c>/<c>References</c> headers,
/// but the same fields carry a WhatsApp <c>wamid</c> or an SMS provider id just as well — there is
/// nothing email-specific about the shape itself.
/// </summary>
public record InboundMessage(
    ChannelKey Channel,
    Guid ChannelAccountId,
    string ExternalMessageId,
    string? InReplyToExternalId,
    IReadOnlyList<string> ReferenceExternalIds,
    string FromAddress,
    string? FromDisplayName,
    string? Subject,
    string BodyText,
    string? BodyHtml,
    DateTimeOffset SentAt,
    IReadOnlyList<InboundAttachment> Attachments,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// The single ingestion path every inbound channel (email today; WhatsApp, SMS and web forms next)
/// funnels through — customer matching, idempotency, threading and attachment extraction implemented
/// once (Communication Channels / Email channel). Getting this interface right here is what lets
/// CS-302, CS-304 and CS-305 be thin adapters instead of three more copies of the same logic.
/// </summary>
public interface IInboundMessagePipeline
{
    /// <summary>Idempotent. Returns the ticket the message landed on, or null if it was a duplicate.</summary>
    Task<Guid?> IngestAsync(InboundMessage message, CancellationToken ct = default);
}
