using CustomerSupport.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Api.Hubs;

/// <summary>
/// Real-time presence and composing signals for a ticket (Agent Dashboard / Team collaboration).
/// One group per ticket (<c>ticket:{id}</c>). Every method re-checks ticket visibility itself —
/// exactly as CS-303's chat hub is specified to authorise per session, not just on connect — because
/// group membership alone does not prove the caller still has access on every subsequent call.
/// </summary>
[Authorize]
public class CollaborationHub(IAppDbContext db, PresenceTracker presence) : Hub
{
    private static string GroupName(Guid ticketId) => $"ticket:{ticketId}";

    public async Task JoinTicket(Guid ticketId)
    {
        var user = new HubCurrentUser(Context.User);
        var visible = await db.Tickets.WhereBranchAccessible(user).WhereTicketVisible(user)
            .AnyAsync(t => t.Id == ticketId, Context.ConnectionAborted);

        if (!visible)
        {
            throw new HubException("You do not have access to this ticket.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(ticketId));
        await presence.JoinAsync(GroupName(ticketId), Context.ConnectionId, user.UserId!.Value, user.UserName ?? "");
    }

    public async Task LeaveTicket(Guid ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(ticketId));
        await presence.LeaveAsync(GroupName(ticketId), Context.ConnectionId);
    }

    public Task StartComposing(Guid ticketId) =>
        presence.SetComposingAsync(GroupName(ticketId), Context.ConnectionId, isComposing: true);

    public Task StopComposing(Guid ticketId) =>
        presence.SetComposingAsync(GroupName(ticketId), Context.ConnectionId, isComposing: false);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await presence.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
