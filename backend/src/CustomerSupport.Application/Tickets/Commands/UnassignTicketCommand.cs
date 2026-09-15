using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>Clears both the agent and team assignment, returning the ticket to an unowned state.</summary>
[RequirePermission(Permissions.Tickets.Assign)]
public record UnassignTicketCommand(Guid TicketId) : IRequest;

public class UnassignTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    IAgentDirectory agents,
    INotificationDispatcher notifications)
    : IRequestHandler<UnassignTicketCommand>
{
    public async Task Handle(UnassignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.AssignedAgentId is null && ticket.AssignedTeamId is null)
        {
            return;
        }

        var previousAgentId = ticket.AssignedAgentId;
        var previousAgent = previousAgentId is { } prevId ? await agents.GetAsync(prevId, cancellationToken) : null;

        ticket.AssignedAgentId = null;
        ticket.AssignedTeamId = null;
        ticket.AssignedAt = null;

        events.Record(ticket.Id, TicketEventType.Unassigned,
            field: nameof(Ticket.AssignedAgentId),
            oldValue: previousAgentId?.ToString(),
            oldDisplay: previousAgent?.DisplayName.En);

        await db.SaveChangesAsync(cancellationToken);

        if (previousAgentId is { } id)
        {
            await TicketAssignmentNotifications.Unassigned(notifications, ticket, id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
