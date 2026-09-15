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
/// Edits a contact's value or details. The type is deliberately not editable — an email becoming a
/// phone number is not an edit, it is a different contact — so callers add a new one instead.
/// </summary>
[RequirePermission(Permissions.Customers.ManageContacts)]
public record UpdateCustomerContactCommand : IRequest<CustomerContactDto>
{
    public Guid CustomerId { get; init; }
    public Guid ContactId { get; init; }
    public string Value { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? CountryCode { get; init; }
    public string? City { get; init; }
    public string? AddressLine { get; init; }
    public string? PostalCode { get; init; }
    public bool AllowNotifications { get; init; } = true;
    public bool ConfirmDuplicate { get; init; }
}

public class UpdateCustomerContactCommandValidator : AbstractValidator<UpdateCustomerContactCommand>
{
    public UpdateCustomerContactCommandValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(256);
    }
}

public class UpdateCustomerContactCommandHandler(IAppDbContext db)
    : IRequestHandler<UpdateCustomerContactCommand, CustomerContactDto>
{
    private static readonly ContactType[] RoutableTypes =
        [ContactType.Email, ContactType.Mobile, ContactType.Phone, ContactType.WhatsApp];

    public async Task<CustomerContactDto> Handle(UpdateCustomerContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.CustomerContacts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(
                c => c.Id == request.ContactId && c.CustomerId == request.CustomerId && !c.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerContact), request.ContactId);

        // The type-aware format check cannot live in the FluentValidation validator: the request
        // carries no type, since it is immutable and only known once the entity above is loaded.
        if (!ContactValueValidator.IsValid(contact.Type, request.Value))
        {
            // Fully qualified: this file also imports FluentValidation, which declares its own
            // ValidationException — the two would otherwise be ambiguous.
            throw new CustomerSupport.Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    ["Value"] = ["Phone must be 7 to 15 digits, optionally prefixed with '+'."],
                });
        }

        var normalized = ContactNormalizer.Normalize(request.Value) ?? request.Value;
        var valueChanged = normalized != contact.NormalizedValue;

        if (valueChanged && RoutableTypes.Contains(contact.Type) && !request.ConfirmDuplicate)
        {
            // AsNoTracking is required here, not just an optimisation: a tracking query cannot
            // project an owned type (LocalizedText) without also including its full owner entity.
            var existing = await db.CustomerContacts
                .AsNoTracking()
                .Where(c => c.NormalizedValue == normalized && c.Type == contact.Type &&
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

        contact.Value = request.Value;
        contact.NormalizedValue = normalized;
        contact.Label = request.Label;
        contact.CountryCode = request.CountryCode;
        contact.City = request.City;
        contact.AddressLine = request.AddressLine;
        contact.PostalCode = request.PostalCode;
        contact.AllowNotifications = request.AllowNotifications;

        if (valueChanged)
        {
            // A changed address invalidates any earlier proof of ownership.
            contact.IsVerified = false;
            contact.VerifiedAt = null;
        }

        if (contact.IsPrimary)
        {
            AddCustomerContactCommandHandler.SyncDenormalizedColumn(contact.Customer, contact);
        }

        await db.SaveChangesAsync(cancellationToken);

        return AddCustomerContactCommandHandler.ToDto(contact);
    }
}
