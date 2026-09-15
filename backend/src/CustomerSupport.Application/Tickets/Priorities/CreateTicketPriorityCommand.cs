using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Priorities;

/// <summary>Creates a priority, appended to the end of the scale. Use <see cref="ReorderTicketPrioritiesCommand"/> to change its position.</summary>
[RequirePermission(Permissions.Tickets.ManagePriorities)]
public record CreateTicketPriorityCommand : IRequest<Guid>
{
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#64748B";
    public string? Icon { get; init; }
    public bool IsDefault { get; init; }
}

public class CreateTicketPriorityCommandValidator : AbstractValidator<CreateTicketPriorityCommand>
{
    public CreateTicketPriorityCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")
            .WithMessage("ColorHex must be a hex colour, e.g. #64748B.");
    }
}

public class CreateTicketPriorityCommandHandler(IAppDbContext db) : IRequestHandler<CreateTicketPriorityCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        if (await db.TicketPriorities.AnyAsync(p => p.Code == request.Code, cancellationToken))
        {
            throw new ConflictException($"A priority with code '{request.Code}' already exists.");
        }

        if (request.IsDefault)
        {
            await db.TicketPriorities.Where(p => p.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), cancellationToken);
        }

        var maxLevel = await db.TicketPriorities.Select(p => (int?)p.Level).MaxAsync(cancellationToken) ?? 0;

        var priority = new TicketPriority
        {
            Code = request.Code,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            Level = maxLevel + 1,
            ColorHex = request.ColorHex,
            Icon = request.Icon,
            IsDefault = request.IsDefault,
            IsActive = true,
        };

        db.TicketPriorities.Add(priority);
        await db.SaveChangesAsync(cancellationToken);
        return priority.Id;
    }
}
