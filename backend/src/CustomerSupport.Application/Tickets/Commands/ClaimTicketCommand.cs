using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Assigns an unassigned ticket to the caller. A conditional update, not read-then-write — two
/// agents watching the same queue can click at the same moment, and only one may win.
/// </summary>
[RequirePermission(Permissions.Tickets.Assign)]
public record ClaimTicketCommand(Guid TicketId) : IRequest;

public class ClaimTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    IUserDisplayNameResolver userNames,
    IDateTimeProvider clock)
    : IRequestHandler<ClaimTicketCommand>
{
    public async Task Handle(ClaimTicketCommand request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        // Only succeeds if the ticket is still unassigned at the moment the UPDATE runs — the
        // database, not this process, is what arbitrates the race.
        var updated = await db.Tickets
            .Where(t => t.Id == request.TicketId && t.AssignedAgentId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AssignedAgentId, currentUser.UserId)
                .SetProperty(t => t.AssignedAt, clock.UtcNow), cancellationToken);

        if (updated == 0)
        {
            var holderId = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => t.AssignedAgentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (holderId is null)
            {
                throw new ConflictException("This ticket is no longer available.");
            }

            var names = await userNames.ResolveAsync([holderId.Value], cancellationToken);
            var holderName = names.TryGetValue(holderId.Value, out var name) ? name.En : "another agent";
            throw new ConflictException($"This ticket was already claimed by {holderName}.");
        }

        // The conditional update above already committed the assignment; the event is a second,
        // separate write, only reached once we know the claim actually succeeded.
        events.Record(request.TicketId, TicketEventType.Assigned,
            field: nameof(Ticket.AssignedAgentId),
            newValue: currentUser.UserId?.ToString(),
            newDisplay: currentUser.UserName,
            triggeredByRule: "Claim");

        await db.SaveChangesAsync(cancellationToken);
    }
}
