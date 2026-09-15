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

/// <summary>
/// Edits a status's names, colour, flags and default state. <c>Code</c> is immutable once created.
/// Changing <c>Kind</c> while tickets currently sit in this status silently changes their SLA and
/// reporting behaviour, so it is refused unless <see cref="Force"/> is set — the caller is expected
/// to have shown the affected count from <see cref="TicketStatusAdminDto.TicketCount"/> first.
/// </summary>
[RequirePermission(Permissions.Tickets.ManageStatuses)]
public record UpdateTicketStatusCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public TicketStatusKind Kind { get; init; }
    public string ColorHex { get; init; } = "#64748B";
    public bool IsTerminal { get; init; }
    public bool PausesSla { get; init; }
    public bool IsDefault { get; init; }
    public bool IsVisibleInPortal { get; init; }
    public bool IsActive { get; init; }
    public bool Force { get; init; }
}

public class UpdateTicketStatusCommandValidator : AbstractValidator<UpdateTicketStatusCommand>
{
    public UpdateTicketStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")
            .WithMessage("ColorHex must be a hex colour, e.g. #64748B.");
    }
}

public class UpdateTicketStatusCommandHandler(IAppDbContext db) : IRequestHandler<UpdateTicketStatusCommand>
{
    public async Task Handle(UpdateTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketStatus), request.Id);

        if (status.IsDefault && !request.IsDefault)
        {
            var anotherDefaultExists = await db.TicketStatuses
                .AnyAsync(s => s.Id != request.Id && s.IsDefault, cancellationToken);

            if (!anotherDefaultExists)
            {
                throw new ConflictException(
                    "Cannot clear the default flag from the only default status. Set another status as default first.");
            }
        }

        if (status.Kind == TicketStatusKind.Closed && request.Kind != TicketStatusKind.Closed)
        {
            var anotherClosedExists = await db.TicketStatuses
                .AnyAsync(s => s.Id != request.Id && s.Kind == TicketStatusKind.Closed && s.IsActive, cancellationToken);

            if (!anotherClosedExists)
            {
                throw new ConflictException(
                    "Cannot change the kind of the only Closed-kind status. At least one must remain.");
            }
        }

        if (status.Kind != request.Kind && !request.Force)
        {
            var ticketCount = await db.Tickets.CountAsync(t => t.StatusId == request.Id, cancellationToken);
            if (ticketCount > 0)
            {
                throw new StatusKindChangeWarningException(
                    $"{ticketCount} ticket(s) are currently in this status. Changing its kind changes their " +
                    "SLA and reporting behaviour immediately. Resubmit with Force to proceed anyway.",
                    ticketCount);
            }
        }

        if (request.IsDefault && !status.IsDefault)
        {
            await db.TicketStatuses.Where(s => s.Id != request.Id && s.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), cancellationToken);
        }

        status.Name = new LocalizedText(request.NameEn, request.NameAr);
        status.Kind = request.Kind;
        status.ColorHex = request.ColorHex;
        status.IsTerminal = request.IsTerminal;
        status.PausesSla = request.PausesSla;
        status.IsDefault = request.IsDefault;
        status.IsVisibleInPortal = request.IsVisibleInPortal;
        status.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
