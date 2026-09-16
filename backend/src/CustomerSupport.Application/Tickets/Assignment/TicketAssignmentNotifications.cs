using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Application.Tickets.Assignment;

/// <summary>
/// The notification copy for assignment changes, centralised so Assign, Claim, Unassign, bulk
/// assignment and automatic assignment (CS-502, from Infrastructure) all read the same wording — and
/// so CS-504 (real channel fan-out) only has one place to touch once <see cref="INotificationDispatcher"/>
/// grows beyond the in-app row. Public so the assignment engine can reuse it across the layer boundary.
/// </summary>
public static class TicketAssignmentNotifications
{
    public static Task Assigned(INotificationDispatcher notifications, Ticket ticket, Guid agentId, CancellationToken ct) =>
        notifications.DispatchAsync(
            agentId,
            "ticket.assigned",
            "You've been assigned a ticket",
            "تم إسناد تذكرة إليك",
            $"Ticket {ticket.Number}: {ticket.Subject}",
            $"التذكرة {ticket.Number}: {ticket.Subject}",
            link: $"/agent/tickets/{ticket.Id}",
            ct: ct);

    public static Task Unassigned(INotificationDispatcher notifications, Ticket ticket, Guid agentId, CancellationToken ct) =>
        notifications.DispatchAsync(
            agentId,
            "ticket.unassigned",
            "A ticket is no longer assigned to you",
            "لم تعد إحدى التذاكر مسندة إليك",
            $"Ticket {ticket.Number} is no longer assigned to you.",
            $"لم تعد التذكرة {ticket.Number} مسندة إليك.",
            link: $"/agent/tickets/{ticket.Id}",
            ct: ct);
}
