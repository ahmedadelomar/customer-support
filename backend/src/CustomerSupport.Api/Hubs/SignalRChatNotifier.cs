using CustomerSupport.Application.Channels.LiveChat;
using Microsoft.AspNetCore.SignalR;

namespace CustomerSupport.Api.Hubs;

/// <summary>The Api-side implementation of <see cref="IChatRealtimeNotifier"/> — see that interface for why this lives here rather than in Application.</summary>
public class SignalRChatNotifier(IHubContext<ChatHub> hub) : IChatRealtimeNotifier
{
    public Task PushToSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group($"chat:{sessionId}").SendAsync(eventName, payload, ct);

    public Task PushToTeamQueueAsync(Guid teamId, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group($"chatqueue:{teamId}").SendAsync(eventName, payload, ct);
}
