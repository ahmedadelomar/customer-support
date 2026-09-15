using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Priorities;

/// <summary>Rewrites <c>Level</c> for every priority from the submitted order — dragging a row in the admin list.</summary>
[RequirePermission(Permissions.Tickets.ManagePriorities)]
public record ReorderTicketPrioritiesCommand : IRequest
{
    /// <summary>Every priority id, highest urgency first.</summary>
    public IReadOnlyList<Guid> OrderedIds { get; init; } = Array.Empty<Guid>();
}

public class ReorderTicketPrioritiesCommandValidator : AbstractValidator<ReorderTicketPrioritiesCommand>
{
    public ReorderTicketPrioritiesCommandValidator()
    {
        RuleFor(x => x.OrderedIds).NotEmpty();
    }
}

public class ReorderTicketPrioritiesCommandHandler(IAppDbContext db) : IRequestHandler<ReorderTicketPrioritiesCommand>
{
    public async Task Handle(ReorderTicketPrioritiesCommand request, CancellationToken cancellationToken)
    {
        var priorities = await db.TicketPriorities
            .Where(p => request.OrderedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            if (priorities.TryGetValue(request.OrderedIds[i], out var priority))
            {
                priority.Level = i + 1;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
