using CustomerSupport.Application.Common;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Contacts;

/// <summary>
/// Adds a contact to an existing customer. A cross-customer duplicate is a warning, not a block:
/// households and companies legitimately share a number, so the caller must explicitly confirm
/// with <see cref="ConfirmDuplicate"/> before the duplicate is accepted.
/// </summary>
[RequirePermission(Permissions.Customers.ManageContacts)]
public record AddCustomerContactCommand : IRequest<CustomerContactDto>
{
    public Guid CustomerId { get; init; }
    public ContactType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? CountryCode { get; init; }
    public string? City { get; init; }
    public string? AddressLine { get; init; }
    public string? PostalCode { get; init; }
    public bool ConfirmDuplicate { get; init; }
}

public class AddCustomerContactCommandValidator : AbstractValidator<AddCustomerContactCommand>
{
    public AddCustomerContactCommandValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(256);

        RuleFor(x => x.Value).EmailAddress()
            .When(x => x.Type == ContactType.Email)
            .WithMessage("Enter a valid email address.");

        // Checked against the NORMALISED value — the raw input legitimately contains spaces
        // ("+966 50 123 4567" is exactly what a customer types), and ContactNormalizer strips them.
        RuleFor(x => x.Value).Must(ContactValueValidator.IsValidPhone)
            .When(x => x.Type is ContactType.Mobile or ContactType.Phone or ContactType.WhatsApp)
            .WithMessage("Phone must be 7 to 15 digits, optionally prefixed with '+'.");

        RuleFor(x => x.AddressLine).NotEmpty()
            .When(x => x.Type == ContactType.Address)
            .WithMessage("AddressLine is required for an address contact.");
    }
}

public class AddCustomerContactCommandHandler(IAppDbContext db)
    : IRequestHandler<AddCustomerContactCommand, CustomerContactDto>
{
    /// <summary>Types inbound routing matches on. Duplicate checking exists only for these.</summary>
    private static readonly ContactType[] RoutableTypes =
        [ContactType.Email, ContactType.Mobile, ContactType.Phone, ContactType.WhatsApp];

    public async Task<CustomerContactDto> Handle(AddCustomerContactCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var normalized = ContactNormalizer.Normalize(request.Value) ?? request.Value;

        if (RoutableTypes.Contains(request.Type) && !request.ConfirmDuplicate)
        {
            // AsNoTracking is required here, not just an optimisation: a tracking query cannot
            // project an owned type (LocalizedText) without also including its full owner entity.
            var existing = await db.CustomerContacts
                .AsNoTracking()
                .Where(c => c.NormalizedValue == normalized && c.Type == request.Type &&
                            c.CustomerId != request.CustomerId && !c.IsDeleted)
                .Select(c => new { c.CustomerId, c.Customer.DisplayName })
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                throw new DuplicateContactException(
                    existing.CustomerId, existing.DisplayName.En,
                    $"This value is already used by another customer ({existing.DisplayName.En}).");
            }
        }

        // The first contact of a type becomes primary by default, so a customer never ends up with
        // a type that has no primary. Later contacts of the same type stay non-primary unless the
        // agent explicitly promotes one via SetPrimaryContactCommand.
        var isFirstOfType = !await db.CustomerContacts
            .AnyAsync(c => c.CustomerId == request.CustomerId && c.Type == request.Type && !c.IsDeleted, cancellationToken);

        var contact = new CustomerContact
        {
            CustomerId = request.CustomerId,
            Type = request.Type,
            Value = request.Value,
            NormalizedValue = normalized,
            Label = request.Label,
            CountryCode = request.CountryCode,
            City = request.City,
            AddressLine = request.AddressLine,
            PostalCode = request.PostalCode,
            IsPrimary = isFirstOfType,
            AllowNotifications = true,
        };

        db.CustomerContacts.Add(contact);

        if (isFirstOfType)
        {
            SyncDenormalizedColumn(customer, contact);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(contact);
    }

    internal static void SyncDenormalizedColumn(Customer customer, CustomerContact contact)
    {
        if (contact.Type == ContactType.Email)
        {
            customer.PrimaryEmail = contact.Value;
        }
        else if (contact.Type is ContactType.Mobile or ContactType.Phone)
        {
            customer.PrimaryPhone = contact.Value;
        }
    }

    internal static CustomerContactDto ToDto(CustomerContact c) => new()
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
    };
}
