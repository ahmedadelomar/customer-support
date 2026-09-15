using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Priorities;

/// <summary>
/// Edits a priority's names, colour, icon, default flag and active state. <c>Code</c> is immutable
/// once created (it is referenced by <c>SlaTarget</c> seed data and configuration by convention),
/// and <c>Level</c> only changes through <see cref="ReorderTicketPrioritiesCommand"/>.
/// </summary>
[RequirePermission(Permissions.Tickets.ManagePriorities)]
public record UpdateTicketPriorityCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#64748B";
    public string? Icon { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
}

public class UpdateTicketPriorityCommandValidator : AbstractValidator<UpdateTicketPriorityCommand>
{
    public UpdateTicketPriorityCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")
            .WithMessage("ColorHex must be a hex colour, e.g. #64748B.");
    }
}

public class UpdateTicketPriorityCommandHandler(IAppDbContext db) : IRequestHandler<UpdateTicketPriorityCommand>
{
    public async Task Handle(UpdateTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        var priority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketPriority), request.Id);

        if (priority.IsDefault && !request.IsDefault)
        {
            var anotherDefaultExists = await db.TicketPriorities
                .AnyAsync(p => p.Id != request.Id && p.IsDefault, cancellationToken);

            if (!anotherDefaultExists)
            {
                throw new ConflictException(
                    "Cannot clear the default flag from the only default priority. Set another priority as default first.");
            }
        }

        if (request.IsDefault && !priority.IsDefault)
        {
            await db.TicketPriorities.Where(p => p.Id != request.Id && p.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), cancellationToken);
        }

        priority.Name = new LocalizedText(request.NameEn, request.NameAr);
        priority.ColorHex = request.ColorHex;
        priority.Icon = request.Icon;
        priority.IsDefault = request.IsDefault;
        priority.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
