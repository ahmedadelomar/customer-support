namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>A single chat message, as returned to either the widget or the agent console.</summary>
public record ChatMessageDto(
    Guid Id,
    Guid ChatSessionId,
    int AuthorType,
    Guid? AuthorId,
    string? AuthorDisplayName,
    string Body,
    bool IsSystemMessage,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt);

/// <summary>One row in the agent queue/active list, or the widget's own session status.</summary>
public record ChatSessionDto(
    Guid Id,
    string VisitorKey,
    string? VisitorName,
    string? VisitorEmail,
    Guid? CustomerId,
    Guid? TicketId,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    Guid? QueuedForTeamId,
    string Status,
    string Language,
    string? PageUrl,
    DateTimeOffset StartedAt,
    DateTimeOffset? FirstAgentReplyAt,
    DateTimeOffset? EndedAt,
    int MessageCount,
    int UnreadCount,
    int? Rating);

public record StartChatSessionRequest(
    Guid ChannelAccountId,
    string? VisitorKey,
    string? VisitorName,
    string? PageUrl,
    string Language,
    string? InitialMessage);

public record StartChatSessionResult(Guid SessionId, string VisitorKey, string Token, ChatSessionDto Session);

public record IdentifyChatSessionRequest(string? Name, string? Email, string? Phone);

public record RateChatSessionRequest(int Rating);
