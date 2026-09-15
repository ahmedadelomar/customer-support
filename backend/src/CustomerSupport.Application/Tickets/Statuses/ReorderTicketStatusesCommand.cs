using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Statuses;

/// <summary>Rewrites <c>DisplayOrder</c> for every status from the submitted order — dragging a row in the admin list.</summary>
[RequirePermission(Permissions.Tickets.ManageStatuses)]
public record ReorderTicketStatusesCommand : IRequest
{
    public IReadOnlyList<Guid> OrderedIds { get; init; } = Array.Empty<Guid>();
}

public class ReorderTicketStatusesCommandValidator : AbstractValidator<ReorderTicketStatusesCommand>
{
    public ReorderTicketStatusesCommandValidator()
    {
        RuleFor(x => x.OrderedIds).NotEmpty();
    }
}

public class ReorderTicketStatusesCommandHandler(IAppDbContext db) : IRequestHandler<ReorderTicketStatusesCommand>
{
    public async Task Handle(ReorderTicketStatusesCommand request, CancellationToken cancellationToken)
    {
        var statuses = await db.TicketStatuses
            .Where(s => request.OrderedIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            if (statuses.TryGetValue(request.OrderedIds[i], out var status))
            {
                status.DisplayOrder = i + 1;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
