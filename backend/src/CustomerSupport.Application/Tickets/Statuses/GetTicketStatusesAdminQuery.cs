using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Statuses;

/// <summary>The full status list, active and inactive, ordered by display order, for the admin editor.</summary>
[RequirePermission(Permissions.Tickets.ManageStatuses)]
public record GetTicketStatusesAdminQuery : IRequest<IReadOnlyList<TicketStatusAdminDto>>;

public class GetTicketStatusesAdminQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTicketStatusesAdminQuery, IReadOnlyList<TicketStatusAdminDto>>
{
    public async Task<IReadOnlyList<TicketStatusAdminDto>> Handle(
        GetTicketStatusesAdminQuery request, CancellationToken cancellationToken)
    {
        var statuses = await db.TicketStatuses.AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);

        var ticketCounts = await db.Tickets.AsNoTracking()
            .GroupBy(t => t.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.StatusId, g => g.Count, cancellationToken);

        return statuses.Select(s => new TicketStatusAdminDto
        {
            Id = s.Id,
            Code = s.Code,
            NameEn = s.Name.En,
            NameAr = s.Name.Ar,
            Kind = s.Kind,
            ColorHex = s.ColorHex,
            DisplayOrder = s.DisplayOrder,
            IsTerminal = s.IsTerminal,
            PausesSla = s.PausesSla,
            IsDefault = s.IsDefault,
            IsVisibleInPortal = s.IsVisibleInPortal,
            IsActive = s.IsActive,
            TicketCount = ticketCounts.TryGetValue(s.Id, out var count) ? count : 0,
        }).ToList();
    }
}
