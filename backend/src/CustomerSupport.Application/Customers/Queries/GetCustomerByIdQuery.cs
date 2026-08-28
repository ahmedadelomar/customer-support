using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Queries;

/// <summary>Full profile plus contacts and header counts, for the customer details page.</summary>
[RequirePermission(Permissions.Customers.View)]
public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDetailDto>;

public class GetCustomerByIdQueryHandler(IAppDbContext db)
    : IRequestHandler<GetCustomerByIdQuery, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Contacts.Where(x => !x.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Customers.Customer), request.Id);

        var ticketCounts = await db.Tickets
            .Where(t => t.CustomerId == request.Id)
            .GroupBy(t => t.Status.IsTerminal)
            .Select(g => new { IsTerminal = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var noteCount = await db.CustomerNotes
            .CountAsync(n => n.CustomerId == request.Id && !n.IsDeleted, cancellationToken);

        return new CustomerDetailDto
        {
            Id = customer.Id,
            Code = customer.Code,
            Type = customer.Type,
            DisplayNameEn = customer.DisplayName.En,
            DisplayNameAr = customer.DisplayName.Ar,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            CompanyName = customer.CompanyName,
            NationalIdOrCr = customer.NationalIdOrCr,
            TaxNumber = customer.TaxNumber,
            PrimaryEmail = customer.PrimaryEmail,
            PrimaryPhone = customer.PrimaryPhone,
            PreferredLanguage = customer.PreferredLanguage,
            PreferredChannel = customer.PreferredChannel,
            TimeZoneId = customer.TimeZoneId,
            Tier = customer.Tier,
            AccountManagerId = customer.AccountManagerId,
            BranchId = customer.BranchId,
            IsActive = customer.IsActive,
            IsBlocked = customer.IsBlocked,
            BlockedReason = customer.BlockedReason,
            SatisfactionScore = customer.SatisfactionScore,
            LastInteractionAt = customer.LastInteractionAt,
            CreatedAt = customer.CreatedAt,
            ModifiedAt = customer.ModifiedAt,
            NoteCount = noteCount,
            TotalTicketCount = ticketCounts.Sum(x => x.Count),
            OpenTicketCount = ticketCounts.Where(x => !x.IsTerminal).Sum(x => x.Count),
            Contacts = customer.Contacts
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Type)
                .Select(x => new CustomerContactDto
                {
                    Id = x.Id,
                    Type = x.Type,
                    Value = x.Value,
                    Label = x.Label,
                    IsPrimary = x.IsPrimary,
                    IsVerified = x.IsVerified,
                    AllowNotifications = x.AllowNotifications,
                    CountryCode = x.CountryCode,
                    City = x.City,
                    AddressLine = x.AddressLine,
                    PostalCode = x.PostalCode,
                })
                .ToList(),
        };
    }
}
