using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Domain.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

[RequirePermission(Permissions.Channels.HandleLiveChat)]
public record AcceptChatSessionCommand(Guid SessionId) : IRequest<ChatSessionDto>;

public class AcceptChatSessionCommandHandler(
    IAppDbContext db, ICurrentUser currentUser, ISettingsProvider settings,
    IChatRealtimeNotifier realtime)
    : IRequestHandler<AcceptChatSessionCommand, ChatSessionDto>
{
    public async Task<ChatSessionDto> Handle(AcceptChatSessionCommand command, CancellationToken ct)
    {
        var agentId = currentUser.UserId!.Value;

        var maxConcurrent = await settings.GetAsync(SettingKeys.LiveChatMaxConcurrentSessions, 3, currentUser.BranchId, ct);
        var activeCount = await db.ChatSessions.CountAsync(s => s.AssignedAgentId == agentId && s.Status == "Active", ct);

        if (activeCount >= maxConcurrent)
        {
            throw new ConflictException($"You are already handling {activeCount} chat(s), the maximum allowed.");
        }

        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException(nameof(ChatSession), command.SessionId);

        // The same claim-by-conditional-update pattern CS-203 (ticket claiming) and round-robin
        // assignment use: an update whose WHERE re-checks the still-unaccepted state can never let
        // two agents both "win" the same session, no matter how close together they click.
        var claimed = await db.ChatSessions
            .Where(s => s.Id == command.SessionId && s.AssignedAgentId == null && s.Status == "Waiting")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.AssignedAgentId, agentId)
                .SetProperty(x => x.Status, "Active")
                .SetProperty(x => x.FirstAgentReplyAt, (DateTimeOffset?)null), ct);

        if (claimed == 0)
        {
            throw new ConflictException("This chat has already been accepted.");
        }

        session.AssignedAgentId = agentId;
        session.Status = "Active";

        var dto = ChatMapper.ToDto(session, currentUser.UserName, 0);
        await realtime.PushToSessionAsync(session.Id, "session.accepted", dto, ct);
        if (session.QueuedForTeamId is { } teamId)
        {
            await realtime.PushToTeamQueueAsync(teamId, "session.accepted", dto, ct);
        }

        return dto;
    }
}
