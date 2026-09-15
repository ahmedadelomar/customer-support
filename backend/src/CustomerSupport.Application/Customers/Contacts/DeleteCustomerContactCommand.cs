using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Contacts;

/// <summary>
/// Removes a contact. Refused when it would leave the customer with no email and no phone — an
/// unreachable customer is a data-quality problem no other safeguard in the system catches.
/// </summary>
[RequirePermission(Permissions.Customers.ManageContacts)]
public record DeleteCustomerContactCommand(Guid CustomerId, Guid ContactId) : IRequest;

public class DeleteCustomerContactCommandHandler(IAppDbContext db)
    : IRequestHandler<DeleteCustomerContactCommand>
{
    /// <summary>The types that count as "reachable" for the always-keep-one-contact rule.</summary>
    private static readonly ContactType[] ReachableTypes = [ContactType.Email, ContactType.Mobile, ContactType.Phone];

    public async Task Handle(DeleteCustomerContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.CustomerContacts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(
                c => c.Id == request.ContactId && c.CustomerId == request.CustomerId && !c.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerContact), request.ContactId);

        if (ReachableTypes.Contains(contact.Type))
        {
            var otherReachableCount = await db.CustomerContacts.CountAsync(
                c => c.CustomerId == request.CustomerId && c.Id != request.ContactId &&
                     !c.IsDeleted && ReachableTypes.Contains(c.Type),
                cancellationToken);

            if (otherReachableCount == 0)
            {
                throw new ConflictException("A customer must retain at least one email or phone contact.");
            }
        }

        var wasPrimary = contact.IsPrimary;
        var type = contact.Type;

        // Remove() is converted to a soft delete by AuditableEntityInterceptor.
        db.CustomerContacts.Remove(contact);

        if (wasPrimary)
        {
            // Promote the oldest survivor of the same type so it never goes without a primary.
            var successor = await db.CustomerContacts
                .Where(c => c.CustomerId == request.CustomerId && c.Type == type &&
                            c.Id != request.ContactId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (successor is not null)
            {
                successor.IsPrimary = true;
                AddCustomerContactCommandHandler.SyncDenormalizedColumn(contact.Customer, successor);
            }
            else if (type == ContactType.Email)
            {
                contact.Customer.PrimaryEmail = null;
            }
            else if (type is ContactType.Mobile or ContactType.Phone)
            {
                contact.Customer.PrimaryPhone = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
