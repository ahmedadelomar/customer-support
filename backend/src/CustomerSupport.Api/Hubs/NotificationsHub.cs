using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CustomerSupport.Api.Hubs;

/// <summary>
/// Real-time push for one user's own notifications (SLA and Automation / Alerts and notifications).
/// Every connection joins its own per-user group on connect — there is nothing to opt into, unlike
/// <see cref="CollaborationHub"/>'s per-ticket groups, since a user only ever needs their own stream.
/// </summary>
[Authorize]
public class NotificationsHub : Hub
{
    public static string GroupName(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var user = new HubCurrentUser(Context.User);
        if (user.UserId is { } userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        }

        await base.OnConnectedAsync();
    }
}
