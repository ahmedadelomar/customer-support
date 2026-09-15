using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>The ticket's watchers list, for the properties-panel sidebar.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetTicketWatchersQuery(Guid TicketId) : IRequest<IReadOnlyList<WatcherDto>>;

public class GetTicketWatchersQueryHandler(IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetTicketWatchersQuery, IReadOnlyList<WatcherDto>>
{
    public async Task<IReadOnlyList<WatcherDto>> Handle(GetTicketWatchersQuery request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        var watchers = await db.TicketWatchers.AsNoTracking()
            .Where(w => w.TicketId == request.TicketId)
            .OrderBy(w => w.AddedAt)
            .ToListAsync(cancellationToken);

        var names = await userNames.ResolveAsync(watchers.Select(w => w.UserId).Distinct(), cancellationToken);

        return watchers.Select(w =>
        {
            var name = names.GetValueOrDefault(w.UserId);
            return new WatcherDto
            {
                UserId = w.UserId,
                NameEn = name?.En ?? "",
                NameAr = name?.Ar ?? "",
                AddedByAutomation = w.AddedByAutomation,
                AddedAt = w.AddedAt,
                IsCurrentUser = w.UserId == currentUser.UserId,
            };
        }).ToList();
    }
}
