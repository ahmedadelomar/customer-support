using CustomerSupport.Api.Chat;
using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Api.Hubs;

/// <summary>
/// Live chat transport (Communication Channels / Live chat, CS-303). Groups per session
/// (<c>chat:{id}</c>, joined by whoever is in that one conversation) and per team
/// (<c>chatqueue:{teamId}</c>, joined by agents watching that team's waiting list).
/// </summary>
/// <remarks>
/// Every method re-checks <see cref="ChatAccess"/> itself, not just on connect — a visitor's token
/// names exactly one session, and nothing here trusts group membership as proof of continued access,
/// same reasoning as <see cref="CollaborationHub"/>. Messages are persisted before they are
/// broadcast, so a client that reconnects and re-fetches the transcript never sees a gap.
/// </remarks>
[Authorize]
public class ChatHub(IAppDbContext db, IDateTimeProvider clock) : Hub
{
    private static string SessionGroup(Guid sessionId) => $"chat:{sessionId}";
    private static string TeamGroup(Guid teamId) => $"chatqueue:{teamId}";

    public Task JoinSession(Guid sessionId) =>
        ChatAccess.CanAccess(Context.User, sessionId)
            ? Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId))
            : throw new HubException("You do not have access to this chat session.");

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));

    /// <summary>Not one of the story's five hub methods, but required for the queue itself to update live: an agent must join a team's queue group before <c>ChatQueuedForTeamId</c> broadcasts reach them.</summary>
    public Task JoinTeamQueue(Guid teamId) =>
        ChatAccess.IsAgent(Context.User)
            ? Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId))
            : throw new HubException("You do not have access to the chat queue.");

    public Task LeaveTeamQueue(Guid teamId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TeamGroup(teamId));

    public async Task SendMessage(Guid sessionId, string body)
    {
        if (!ChatAccess.CanAccess(Context.User, sessionId))
        {
            throw new HubException("You do not have access to this chat session.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var ct = Context.ConnectionAborted;
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new HubException("Chat session not found.");

        var isAgent = ChatAccess.IsAgent(Context.User);
        Guid? authorId = null;
        string? authorDisplayName;

        if (isAgent)
        {
            var agent = new HubCurrentUser(Context.User);
            authorId = agent.UserId;
            authorDisplayName = agent.UserName;
            session.FirstAgentReplyAt ??= clock.UtcNow;
        }
        else
        {
            authorDisplayName = session.VisitorName ?? "Visitor";
        }

        // Stored and later shown through the console's plain-text binding — never innerHTML — which
        // is what actually satisfies "visitor input is untrusted, render as text": there is nothing
        // here to sanitize because nothing here is ever interpreted as markup.
        var message = new ChatMessage
        {
            ChatSessionId = sessionId,
            AuthorType = isAgent ? MessageAuthorType.Agent : MessageAuthorType.Customer,
            AuthorId = authorId,
            AuthorDisplayName = authorDisplayName,
            Body = body,
            SentAt = clock.UtcNow,
        };

        db.ChatMessages.Add(message);
        session.MessageCount += 1;
        await db.SaveChangesAsync(ct);

        await Clients.Group(SessionGroup(sessionId)).SendAsync("messageReceived", ChatMapper.ToDto(message), ct);
    }

    public Task Typing(Guid sessionId, bool isTyping) =>
        ChatAccess.CanAccess(Context.User, sessionId)
            ? Clients.OthersInGroup(SessionGroup(sessionId)).SendAsync("typing", new { sessionId, isTyping }, Context.ConnectionAborted)
            : Task.CompletedTask;

    public async Task MarkRead(Guid sessionId)
    {
        if (!ChatAccess.CanAccess(Context.User, sessionId))
        {
            return;
        }

        var ct = Context.ConnectionAborted;

        // An agent marks the visitor's messages read, and vice versa — never one's own.
        var authorTypeToMark = ChatAccess.IsAgent(Context.User) ? MessageAuthorType.Customer : MessageAuthorType.Agent;

        var unread = await db.ChatMessages
            .Where(m => m.ChatSessionId == sessionId && m.ReadAt == null && m.AuthorType == authorTypeToMark)
            .ToListAsync(ct);

        if (unread.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var message in unread)
        {
            message.ReadAt = now;
        }

        await db.SaveChangesAsync(ct);

        await Clients.Group(SessionGroup(sessionId))
            .SendAsync("messagesRead", new { sessionId, messageIds = unread.Select(m => m.Id) }, ct);
    }
}
