using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Statuses;

/// <summary>Permanently deletes a status. Refused while any ticket references it, it is the default, or it is the last Closed-kind status.</summary>
[RequirePermission(Permissions.Tickets.ManageStatuses)]
public record DeleteTicketStatusCommand(Guid Id) : IRequest;

public class DeleteTicketStatusCommandHandler(IAppDbContext db) : IRequestHandler<DeleteTicketStatusCommand>
{
    public async Task Handle(DeleteTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketStatus), request.Id);

        if (status.IsDefault)
        {
            throw new ConflictException("Cannot delete the default status. Set another status as default first.");
        }

        var ticketCount = await db.Tickets.CountAsync(t => t.StatusId == request.Id, cancellationToken);
        if (ticketCount > 0)
        {
            throw new ConflictException(
                $"Cannot delete this status while {ticketCount} ticket(s) reference it. Deactivate it instead.");
        }

        if (status.Kind == TicketStatusKind.Closed)
        {
            var anotherClosedExists = await db.TicketStatuses
                .AnyAsync(s => s.Id != request.Id && s.Kind == TicketStatusKind.Closed && s.IsActive, cancellationToken);

            if (!anotherClosedExists)
            {
                throw new ConflictException("Cannot delete the only Closed-kind status. At least one must remain.");
            }
        }

        db.TicketStatuses.Remove(status);
        await db.SaveChangesAsync(cancellationToken);
    }
}
