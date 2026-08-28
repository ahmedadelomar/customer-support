using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Tickets;

/// <summary>
/// One entry in the ticket conversation: a customer message, an agent reply, or an internal note.
/// Channel adapters create these on ingestion and read them back when sending outbound.
/// </summary>
public class TicketMessage : BaseEntity, IAuditable, ISoftDeletable
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public ChannelKey Channel { get; set; }
    public MessageDirection Direction { get; set; }
    public MessageAuthorType AuthorType { get; set; }

    /// <summary>Agent user id or customer id, depending on <see cref="AuthorType"/>.</summary>
    public Guid? AuthorId { get; set; }
    public string? AuthorDisplayName { get; set; }

    public string? Subject { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }

    /// <summary>Internal notes are visible to agents only: never sent outbound, never shown in the portal.</summary>
    public bool IsInternalNote { get; set; }

    /// <summary>Provider id such as an email Message-ID or WhatsApp wamid, used for idempotent ingestion and threading.</summary>
    public string? ExternalMessageId { get; set; }
    public string? InReplyToExternalId { get; set; }

    public Guid? QuickReplyId { get; set; }

    /// <summary>Set when an agent sent this reply from an AI suggestion, for accept-rate reporting.</summary>
    public Guid? AiSuggestionId { get; set; }

    public int AttachmentCount { get; set; }
    public DateTimeOffset SentAt { get; set; }

    public ICollection<MessageDeliveryLog> DeliveryLogs { get; set; } = new List<MessageDeliveryLog>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
