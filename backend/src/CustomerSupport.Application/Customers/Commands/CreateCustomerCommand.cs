using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Commands;

/// <summary>
/// Creates a customer profile together with its primary email and phone contacts, so a new customer
/// is immediately reachable rather than needing a second call.
/// </summary>
[RequirePermission(Permissions.Customers.Create)]
public record CreateCustomerCommand : IRequest<Guid>
{
    public CustomerType Type { get; init; } = CustomerType.Individual;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CompanyName { get; init; }
    public string? NationalIdOrCr { get; init; }
    public string? TaxNumber { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public ChannelKey PreferredChannel { get; init; } = ChannelKey.Email;
    public string? TimeZoneId { get; init; }
    public string? Tier { get; init; }
    public Guid? AccountManagerId { get; init; }
    public Guid? BranchId { get; init; }
}

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).Must(l => l is "ar" or "en")
            .WithMessage("PreferredLanguage must be 'ar' or 'en'.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9]{7,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Phone must be 7 to 15 digits, optionally prefixed with '+'.");
        RuleFor(x => x.CompanyName).NotEmpty()
            .When(x => x.Type is CustomerType.Company or CustomerType.Government)
            .WithMessage("CompanyName is required for company and government customers.");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("At least one of Email or Phone must be provided.");
    }
}

public class CreateCustomerCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IReferenceNumberGenerator numbers)
    : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var phone = Normalize(request.Phone);

        // Reject duplicates up front: two profiles for one person is the most common data-quality
        // problem in a support CRM, and it silently splits the interaction history.
        if (email is not null &&
            await db.CustomerContacts.AnyAsync(
                c => c.NormalizedValue == email && c.Type == ContactType.Email && !c.IsDeleted,
                cancellationToken))
        {
            throw new ConflictException($"A customer with email '{request.Email}' already exists.");
        }

        if (phone is not null &&
            await db.CustomerContacts.AnyAsync(
                c => c.NormalizedValue == phone && !c.IsDeleted &&
                     (c.Type == ContactType.Mobile || c.Type == ContactType.Phone),
                cancellationToken))
        {
            throw new ConflictException($"A customer with phone '{request.Phone}' already exists.");
        }

        var customer = new Customer
        {
            Code = await numbers.NextCustomerCodeAsync(cancellationToken),
            Type = request.Type,
            DisplayName = new LocalizedText(request.DisplayNameEn, request.DisplayNameAr),
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            NationalIdOrCr = request.NationalIdOrCr,
            TaxNumber = request.TaxNumber,
            PrimaryEmail = request.Email,
            PrimaryPhone = request.Phone,
            PreferredLanguage = request.PreferredLanguage,
            PreferredChannel = request.PreferredChannel,
            TimeZoneId = request.TimeZoneId,
            Tier = request.Tier,
            AccountManagerId = request.AccountManagerId,
            BranchId = request.BranchId ?? currentUser.BranchId,
            IsActive = true,
        };

        if (email is not null)
        {
            customer.Contacts.Add(new CustomerContact
            {
                Type = ContactType.Email,
                Value = request.Email!,
                NormalizedValue = email,
                IsPrimary = true,
            });
        }

        if (phone is not null)
        {
            customer.Contacts.Add(new CustomerContact
            {
                Type = ContactType.Mobile,
                Value = request.Phone!,
                NormalizedValue = phone,
                IsPrimary = true,
            });
        }

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    /// <summary>Lower-cases emails and strips separators from phone numbers so dedupe is reliable.</summary>
    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Contains('@')
            ? trimmed.ToLowerInvariant()
            : new string(trimmed.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
    }
}
