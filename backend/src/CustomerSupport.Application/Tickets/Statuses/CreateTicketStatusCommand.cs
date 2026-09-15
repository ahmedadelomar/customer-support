using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Statuses;

/// <summary>Creates a status, appended to the end of the workflow order.</summary>
[RequirePermission(Permissions.Tickets.ManageStatuses)]
public record CreateTicketStatusCommand : IRequest<Guid>
{
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public TicketStatusKind Kind { get; init; }
    public string ColorHex { get; init; } = "#64748B";
    public bool IsTerminal { get; init; }
    public bool PausesSla { get; init; }
    public bool IsDefault { get; init; }
    public bool IsVisibleInPortal { get; init; } = true;
}

public class CreateTicketStatusCommandValidator : AbstractValidator<CreateTicketStatusCommand>
{
    public CreateTicketStatusCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")
            .WithMessage("ColorHex must be a hex colour, e.g. #64748B.");
    }
}

public class CreateTicketStatusCommandHandler(IAppDbContext db) : IRequestHandler<CreateTicketStatusCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketStatusCommand request, CancellationToken cancellationToken)
    {
        if (await db.TicketStatuses.AnyAsync(s => s.Code == request.Code, cancellationToken))
        {
            throw new ConflictException($"A status with code '{request.Code}' already exists.");
        }

        if (request.IsDefault)
        {
            await db.TicketStatuses.Where(s => s.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), cancellationToken);
        }

        var maxOrder = await db.TicketStatuses.Select(s => (int?)s.DisplayOrder).MaxAsync(cancellationToken) ?? 0;

        var status = new TicketStatus
        {
            Code = request.Code,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            Kind = request.Kind,
            ColorHex = request.ColorHex,
            DisplayOrder = maxOrder + 1,
            IsTerminal = request.IsTerminal,
            PausesSla = request.PausesSla,
            IsDefault = request.IsDefault,
            IsVisibleInPortal = request.IsVisibleInPortal,
            IsActive = true,
        };

        db.TicketStatuses.Add(status);
        await db.SaveChangesAsync(cancellationToken);
        return status.Id;
    }
}
