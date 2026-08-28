using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Workspace;

/// <summary>
/// An at-mention of a colleague inside an internal note (Agent Dashboard / Team collaboration).
/// Drives the collaboration inbox and the unread badge.
/// </summary>
public class TicketMention : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid TicketMessageId { get; set; }
    public Guid MentionedUserId { get; set; }
    public Guid MentionedById { get; set; }

    public DateTimeOffset MentionedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
