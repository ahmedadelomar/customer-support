using CustomerSupport.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CustomerSupport.Api.Hubs;

/// <summary>The API-side implementation of <see cref="IRealtimeNotifier"/> — see that interface for why this lives here rather than in Infrastructure.</summary>
public class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    public Task NotifyAsync(Guid userId, RealtimeNotification notification, CancellationToken ct = default) =>
        hub.Clients.Group(NotificationsHub.GroupName(userId)).SendAsync("notificationReceived", notification, ct);
}
