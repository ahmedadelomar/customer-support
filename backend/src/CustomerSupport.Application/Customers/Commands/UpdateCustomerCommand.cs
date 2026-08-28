using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Commands;

/// <summary>
/// Updates profile fields. Contacts are managed through their own endpoints, and
/// <c>Code</c> is immutable because it is quoted to customers.
/// </summary>
[RequirePermission(Permissions.Customers.Update)]
public record UpdateCustomerCommand : IRequest
{
    public Guid Id { get; init; }
    public CustomerType Type { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CompanyName { get; init; }
    public string? NationalIdOrCr { get; init; }
    public string? TaxNumber { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public ChannelKey PreferredChannel { get; init; }
    public string? TimeZoneId { get; init; }
    public string? Tier { get; init; }
    public Guid? AccountManagerId { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsBlocked { get; init; }
    public string? BlockedReason { get; init; }
}

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).Must(l => l is "ar" or "en");
        RuleFor(x => x.CompanyName).NotEmpty()
            .When(x => x.Type is CustomerType.Company or CustomerType.Government);
        RuleFor(x => x.BlockedReason).NotEmpty().When(x => x.IsBlocked)
            .WithMessage("A reason is required when blocking a customer.");
    }
}

public class UpdateCustomerCommandHandler(IAppDbContext db) : IRequestHandler<UpdateCustomerCommand>
{
    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Customers.Customer), request.Id);

        customer.Type = request.Type;
        customer.DisplayName = new LocalizedText(request.DisplayNameEn, request.DisplayNameAr);
        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.CompanyName = request.CompanyName;
        customer.NationalIdOrCr = request.NationalIdOrCr;
        customer.TaxNumber = request.TaxNumber;
        customer.PreferredLanguage = request.PreferredLanguage;
        customer.PreferredChannel = request.PreferredChannel;
        customer.TimeZoneId = request.TimeZoneId;
        customer.Tier = request.Tier;
        customer.AccountManagerId = request.AccountManagerId;
        customer.IsActive = request.IsActive;
        customer.IsBlocked = request.IsBlocked;
        customer.BlockedReason = request.IsBlocked ? request.BlockedReason : null;

        await db.SaveChangesAsync(cancellationToken);
    }
}
