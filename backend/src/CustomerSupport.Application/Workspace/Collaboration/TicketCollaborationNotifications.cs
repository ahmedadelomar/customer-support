using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>
/// Notifies a ticket's watchers of new activity (a reply or an internal note) — the effect the
/// story's verification step 4 ("watch a ticket you are not assigned: you receive activity
/// notifications") requires. Scoped deliberately to new messages only, not every ticket mutation:
/// assignment and escalation already have their own targeted notifications
/// (<see cref="Tickets.Assignment.TicketAssignmentNotifications"/>, <c>EscalateTicketCommand</c>), and
/// fanning those out to watchers too would duplicate what the assignee/escalation path already sends.
/// </summary>
internal static class TicketCollaborationNotifications
{
    public static async Task NotifyActivityAsync(
        INotificationDispatcher notifications,
        Ticket ticket,
        IEnumerable<TicketWatcher> watchers,
        Guid actorId,
        string actorDisplayName,
        bool isInternalNote,
        CancellationToken ct)
    {
        var recipients = watchers.Select(w => w.UserId).Where(id => id != actorId).Distinct();

        foreach (var userId in recipients)
        {
            await notifications.DispatchAsync(
                userId,
                "ticket.watched_activity",
                "Activity on a ticket you watch",
                "نشاط في تذكرة تراقبها",
                isInternalNote
                    ? $"{actorDisplayName} added an internal note on ticket {ticket.Number}: {ticket.Subject}"
                    : $"{actorDisplayName} replied on ticket {ticket.Number}: {ticket.Subject}",
                isInternalNote
                    ? $"أضاف {actorDisplayName} ملاحظة داخلية على التذكرة {ticket.Number}: {ticket.Subject}"
                    : $"رد {actorDisplayName} على التذكرة {ticket.Number}: {ticket.Subject}",
                link: $"/agent/tickets/{ticket.Id}",
                ct: ct);
        }
    }
}
