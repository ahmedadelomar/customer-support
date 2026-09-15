using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>
/// The caller's saved ticket-list filters plus their team's shared ones (approximated here as views
/// shared within the same branch — full team-scoped sharing is a CS-1203 concern once team
/// membership is queryable from the caller's own claims).
/// </summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetSavedTicketViewsQuery : IRequest<IReadOnlyList<SavedTicketViewDto>>;

public class GetSavedTicketViewsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSavedTicketViewsQuery, IReadOnlyList<SavedTicketViewDto>>
{
    public async Task<IReadOnlyList<SavedTicketViewDto>> Handle(
        GetSavedTicketViewsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        var views = await db.SavedTicketViews.AsNoTracking()
            .Where(v => v.OwnerId == userId || (v.IsShared && v.BranchId == currentUser.BranchId))
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync(cancellationToken);

        return views.Select(v => new SavedTicketViewDto
        {
            Id = v.Id,
            NameEn = v.Name.En,
            NameAr = v.Name.Ar,
            FiltersJson = v.FiltersJson,
            DisplayOrder = v.DisplayOrder,
            IsShared = v.IsShared,
        }).ToList();
    }
}
