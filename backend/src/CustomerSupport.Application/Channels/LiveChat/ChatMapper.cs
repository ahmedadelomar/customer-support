using CustomerSupport.Domain.Channels;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Shared entity-to-DTO shaping so every handler that returns a session or message agrees on the
/// shape. Public (not internal) because <c>AbandonStaleChatSessionsJob</c> in Infrastructure uses it
/// too, not just the handlers in this project.
/// </summary>
public static class ChatMapper
{
    public static ChatSessionDto ToDto(ChatSession session, string? assignedAgentName, int unreadCount) => new(
        session.Id,
        session.VisitorKey,
        session.VisitorName,
        session.VisitorEmail,
        session.CustomerId,
        session.TicketId,
        session.AssignedAgentId,
        assignedAgentName,
        session.QueuedForTeamId,
        session.Status,
        session.Language,
        session.PageUrl,
        session.StartedAt,
        session.FirstAgentReplyAt,
        session.EndedAt,
        session.MessageCount,
        unreadCount,
        session.Rating);

    public static ChatMessageDto ToDto(ChatMessage message) => new(
        message.Id,
        message.ChatSessionId,
        (int)message.AuthorType,
        message.AuthorId,
        message.AuthorDisplayName,
        message.Body,
        message.IsSystemMessage,
        message.SentAt,
        message.ReadAt);
}
