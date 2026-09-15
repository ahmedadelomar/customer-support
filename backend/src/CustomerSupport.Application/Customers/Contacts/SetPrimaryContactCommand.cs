using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Contacts;

/// <summary>
/// Promotes one contact to primary for its type, demoting whichever was primary before — atomically,
/// so a query can never observe two primaries (or none) of the same type.
/// </summary>
[RequirePermission(Permissions.Customers.ManageContacts)]
public record SetPrimaryContactCommand(Guid CustomerId, Guid ContactId) : IRequest;

public class SetPrimaryContactCommandHandler(IAppDbContext db) : IRequestHandler<SetPrimaryContactCommand>
{
    public async Task Handle(SetPrimaryContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.CustomerContacts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(
                c => c.Id == request.ContactId && c.CustomerId == request.CustomerId && !c.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerContact), request.ContactId);

        if (contact.IsPrimary)
        {
            return;
        }

        await db.CustomerContacts
            .Where(c => c.CustomerId == request.CustomerId && c.Type == contact.Type && c.Id != contact.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPrimary, false), cancellationToken);

        contact.IsPrimary = true;
        AddCustomerContactCommandHandler.SyncDenormalizedColumn(contact.Customer, contact);

        await db.SaveChangesAsync(cancellationToken);
    }
}
