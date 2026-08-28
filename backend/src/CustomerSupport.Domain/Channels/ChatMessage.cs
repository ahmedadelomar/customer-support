using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// A single live-chat message. Kept separate from <c>TicketMessage</c> because chats exist before
/// (and often without) a ticket; promoting a chat copies the transcript across.
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;

    public MessageAuthorType AuthorType { get; set; }
    public Guid? AuthorId { get; set; }
    public string? AuthorDisplayName { get; set; }

    public string Body { get; set; } = string.Empty;
    /// <summary>System messages carry events such as agent joined or chat transferred.</summary>
    public bool IsSystemMessage { get; set; }
    public int AttachmentCount { get; set; }

    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
