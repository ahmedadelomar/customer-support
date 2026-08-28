using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Commands;

/// <summary>
/// Soft-deletes a customer. Refused while open tickets exist, because deleting would orphan live
/// work; the caller is expected to close or reassign those tickets first.
/// </summary>
[RequirePermission(Permissions.Customers.Delete)]
public record DeleteCustomerCommand(Guid Id) : IRequest;

public class DeleteCustomerCommandHandler(IAppDbContext db) : IRequestHandler<DeleteCustomerCommand>
{
    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Customers.Customer), request.Id);

        var openTickets = await db.Tickets
            .CountAsync(t => t.CustomerId == request.Id && !t.Status.IsTerminal, cancellationToken);

        if (openTickets > 0)
        {
            throw new ConflictException(
                $"Cannot delete this customer while {openTickets} open ticket(s) remain. Close or reassign them first.");
        }

        // The soft-delete interceptor turns this into an update and stamps the audit columns.
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
    }
}
