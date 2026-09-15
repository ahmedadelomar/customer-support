using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Priorities;

/// <summary>The full priority scale, active and inactive, ordered by <c>Level</c> for the admin editor.</summary>
[RequirePermission(Permissions.Tickets.ManagePriorities)]
public record GetTicketPrioritiesAdminQuery : IRequest<IReadOnlyList<TicketPriorityAdminDto>>;

public class GetTicketPrioritiesAdminQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTicketPrioritiesAdminQuery, IReadOnlyList<TicketPriorityAdminDto>>
{
    public async Task<IReadOnlyList<TicketPriorityAdminDto>> Handle(
        GetTicketPrioritiesAdminQuery request, CancellationToken cancellationToken)
    {
        var priorities = await db.TicketPriorities.AsNoTracking()
            .OrderBy(p => p.Level)
            .ToListAsync(cancellationToken);

        var ticketCounts = await db.Tickets.AsNoTracking()
            .GroupBy(t => t.PriorityId)
            .Select(g => new { PriorityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PriorityId, g => g.Count, cancellationToken);

        var slaTargetCounts = await db.SlaTargets.AsNoTracking()
            .GroupBy(t => t.PriorityId)
            .Select(g => new { PriorityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PriorityId, g => g.Count, cancellationToken);

        return priorities.Select(p => new TicketPriorityAdminDto
        {
            Id = p.Id,
            Code = p.Code,
            NameEn = p.Name.En,
            NameAr = p.Name.Ar,
            Level = p.Level,
            ColorHex = p.ColorHex,
            Icon = p.Icon,
            IsDefault = p.IsDefault,
            IsActive = p.IsActive,
            TicketCount = ticketCounts.TryGetValue(p.Id, out var tc) ? tc : 0,
            SlaTargetCount = slaTargetCounts.TryGetValue(p.Id, out var sc) ? sc : 0,
        }).ToList();
    }
}
