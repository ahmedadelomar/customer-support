using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

public record ChatQueueDto(IReadOnlyList<ChatSessionDto> Waiting, IReadOnlyList<ChatSessionDto> Active);

[RequirePermission(Permissions.Channels.HandleLiveChat)]
public record GetChatQueueQuery : IRequest<ChatQueueDto>;

public class GetChatQueueQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetChatQueueQuery, ChatQueueDto>
{
    public async Task<ChatQueueDto> Handle(GetChatQueueQuery request, CancellationToken ct)
    {
        var myTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId && m.IsActive)
            .Select(m => m.TeamId)
            .ToListAsync(ct);

        var waiting = await db.ChatSessions
            .Where(s => s.Status == "Waiting" && s.QueuedForTeamId != null && myTeamIds.Contains(s.QueuedForTeamId!.Value))
            .OrderBy(s => s.StartedAt)
            .ToListAsync(ct);

        var active = await db.ChatSessions
            .Where(s => s.Status == "Active" && s.AssignedAgentId == currentUser.UserId)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(ct);

        var sessionIds = waiting.Select(s => s.Id).Concat(active.Select(s => s.Id)).ToList();
        var unreadBySession = await db.ChatMessages
            .Where(m => sessionIds.Contains(m.ChatSessionId) && m.ReadAt == null && m.AuthorType == Domain.Enums.MessageAuthorType.Customer)
            .GroupBy(m => m.ChatSessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, ct);

        ChatSessionDto ToDto(Domain.Channels.ChatSession s) =>
            ChatMapper.ToDto(s, s.AssignedAgentId == currentUser.UserId ? currentUser.UserName : null,
                unreadBySession.GetValueOrDefault(s.Id));

        return new ChatQueueDto(waiting.Select(ToDto).ToList(), active.Select(ToDto).ToList());
    }
}
