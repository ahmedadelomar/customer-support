using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Contacts;

/// <summary>
/// Refetches just the contacts for one customer, so the contact panel can refresh itself after a
/// mutation without reloading the whole profile and losing the agent's place on another tab.
/// </summary>
[RequirePermission(Permissions.Customers.View)]
public record GetCustomerContactsQuery(Guid CustomerId) : IRequest<IReadOnlyList<CustomerContactDto>>;

public class GetCustomerContactsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetCustomerContactsQuery, IReadOnlyList<CustomerContactDto>>
{
    public async Task<IReadOnlyList<CustomerContactDto>> Handle(
        GetCustomerContactsQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Customers.Customer), request.CustomerId);
        }

        return await db.CustomerContacts
            .AsNoTracking()
            .Where(c => c.CustomerId == request.CustomerId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Type)
            .Select(c => new CustomerContactDto
            {
                Id = c.Id,
                Type = c.Type,
                Value = c.Value,
                Label = c.Label,
                IsPrimary = c.IsPrimary,
                IsVerified = c.IsVerified,
                AllowNotifications = c.AllowNotifications,
                CountryCode = c.CountryCode,
                City = c.City,
                AddressLine = c.AddressLine,
                PostalCode = c.PostalCode,
            })
            .ToListAsync(cancellationToken);
    }
}
