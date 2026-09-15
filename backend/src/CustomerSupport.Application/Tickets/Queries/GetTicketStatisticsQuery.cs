using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>KPI tiles above the ticket list. Scoped exactly like <see cref="GetTicketsQuery"/> so the numbers never disagree with the grid.</summary>
[RequirePermission(Permissions.Tickets.View)]
public class GetTicketStatisticsQuery : TicketFilterQuery, IRequest<TicketStatisticsDto>;

public class GetTicketStatisticsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetTicketStatisticsQuery, TicketStatisticsDto>
{
    public async Task<TicketStatisticsDto> Handle(GetTicketStatisticsQuery request, CancellationToken cancellationToken)
    {
        // Applies the same assignment/status/etc. filters as the list (the tiles compute their own
        // due/breached conditions directly, so the SLA-state filter itself is intentionally excluded
        // by leaving request.SlaState at whatever the caller passed — it still applies consistently).
        var baseQuery = db.Tickets.AsNoTracking()
            .WhereBranchAccessible(currentUser)
            .WhereTicketVisible(currentUser);

        baseQuery = await TicketFilters.ApplyAsync(baseQuery, request, currentUser, db, cancellationToken);

        var endOfToday = DateTimeOffset.UtcNow.Date.AddDays(1);

        var openCount = await baseQuery.CountAsync(t => !t.Status.IsTerminal, cancellationToken);
        var unassignedCount = await baseQuery.CountAsync(t => t.AssignedAgentId == null && !t.Status.IsTerminal, cancellationToken);
        var dueTodayCount = await baseQuery.CountAsync(
            t => !t.Status.IsTerminal && t.ResolutionDueAt != null && t.ResolutionDueAt < endOfToday,
            cancellationToken);
        var breachedCount = await baseQuery.CountAsync(
            t => !t.Status.IsTerminal && (t.IsFirstResponseBreached || t.IsResolutionBreached),
            cancellationToken);

        return new TicketStatisticsDto
        {
            OpenCount = openCount,
            UnassignedCount = unassignedCount,
            DueTodayCount = dueTodayCount,
            BreachedCount = breachedCount,
        };
    }
}
