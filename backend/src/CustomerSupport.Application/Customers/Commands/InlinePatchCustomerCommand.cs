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
/// Patches only the fields an agent legitimately corrects in flow, from the ticket screen's customer
/// panel (Agent Dashboard / Customer information) — display names, preferred language, preferred
/// channel and tier. Everything else on the profile stays on the full customer form. Goes through the
/// normal <c>Customer</c> change-tracking path, same as <see cref="UpdateCustomerCommand"/>, so
/// <c>AuditLogInterceptor</c> records it exactly like any other customer edit — no separate audit call
/// is needed here.
/// </summary>
[RequirePermission(Permissions.Customers.Update)]
public record InlinePatchCustomerCommand : IRequest
{
    public Guid Id { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = "ar";
    public ChannelKey PreferredChannel { get; init; }
    public string? Tier { get; init; }
}

/// <summary>Mirrors <see cref="UpdateCustomerCommandValidator"/>'s rules for the same fields, rather than a second set.</summary>
public class InlinePatchCustomerCommandValidator : AbstractValidator<InlinePatchCustomerCommand>
{
    public InlinePatchCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).Must(l => l is "ar" or "en");
    }
}

public class InlinePatchCustomerCommandHandler(IAppDbContext db) : IRequestHandler<InlinePatchCustomerCommand>
{
    public async Task Handle(InlinePatchCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Customers.Customer), request.Id);

        customer.DisplayName = new LocalizedText(request.DisplayNameEn, request.DisplayNameAr);
        customer.PreferredLanguage = request.PreferredLanguage;
        customer.PreferredChannel = request.PreferredChannel;
        customer.Tier = request.Tier;

        await db.SaveChangesAsync(cancellationToken);
    }
}
