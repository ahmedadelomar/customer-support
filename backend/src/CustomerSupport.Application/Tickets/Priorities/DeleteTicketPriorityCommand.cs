using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Priorities;

/// <summary>
/// Permanently deletes a priority — unlike a category, <see cref="TicketPriority"/> is not
/// soft-deletable, so this is refused outright when anything still references it and the caller is
/// directed to deactivate instead.
/// </summary>
[RequirePermission(Permissions.Tickets.ManagePriorities)]
public record DeleteTicketPriorityCommand(Guid Id) : IRequest;

public class DeleteTicketPriorityCommandHandler(IAppDbContext db) : IRequestHandler<DeleteTicketPriorityCommand>
{
    public async Task Handle(DeleteTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        var priority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketPriority), request.Id);

        var slaTargetCount = await db.SlaTargets.CountAsync(t => t.PriorityId == request.Id, cancellationToken);
        if (slaTargetCount > 0)
        {
            throw new ConflictException(
                $"Cannot delete this priority while {slaTargetCount} SLA target(s) reference it. Deactivate it instead.");
        }

        var ticketCount = await db.Tickets.CountAsync(t => t.PriorityId == request.Id, cancellationToken);
        if (ticketCount > 0)
        {
            throw new ConflictException(
                $"Cannot delete this priority while {ticketCount} ticket(s) reference it. Deactivate it instead.");
        }

        if (priority.IsDefault)
        {
            throw new ConflictException("Cannot delete the default priority. Set another priority as default first.");
        }

        db.TicketPriorities.Remove(priority);
        await db.SaveChangesAsync(cancellationToken);
    }
}
