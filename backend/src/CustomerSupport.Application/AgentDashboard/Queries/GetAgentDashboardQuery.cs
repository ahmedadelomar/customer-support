using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.AgentDashboard.Dtos;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.AgentDashboard.Queries;

/// <summary>
/// The agent's home screen (Agent Dashboard / Assigned tickets): tiles, the urgency-ordered next-up
/// queue and personal stats in one response, so the dashboard never fires more than one request on
/// load. Open to any authenticated caller — the queue and stats sections instead degrade
/// independently via <see cref="AgentDashboardDto.CanViewQueue"/>/<c>CanViewStats</c>, per the
/// story's "every section is permission-gated and simply absent when not permitted" rule.
/// </summary>
public record GetAgentDashboardQuery : IRequest<AgentDashboardDto>;

public class GetAgentDashboardQueryHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<GetAgentDashboardQuery, AgentDashboardDto>
{
    public async Task<AgentDashboardDto> Handle(GetAgentDashboardQuery request, CancellationToken cancellationToken)
    {
        var canViewQueue = currentUser.HasPermission(Permissions.Tickets.View);
        var canViewStats = currentUser.HasPermission(Permissions.Reports.ViewAgentPerformance);

        var tiles = new AgentDashboardTilesDto();
        IReadOnlyList<AgentQueueItemDto> queue = Array.Empty<AgentQueueItemDto>();
        var stats = new AgentDashboardStatsDto();

        if (canViewQueue)
        {
            var mine = AgentDashboardQueries.MyOpenTickets(db, currentUser);
            var now = clock.UtcNow;
            var endOfDay = now.Date.AddDays(1);

            tiles = await mine
                .GroupBy(_ => 1)
                .Select(g => new AgentDashboardTilesDto
                {
                    Assigned = g.Count(),
                    DueToday = g.Count(t => t.ResolutionDueAt != null && t.ResolutionDueAt < endOfDay),
                    ApproachingSla = g.Count(t => !t.IsResolutionBreached && t.ResolutionDueAt != null
                        && t.ResolutionDueAt > now && t.ResolutionDueAt < now.AddHours(2)),
                    Breached = g.Count(t => t.IsResolutionBreached || t.IsFirstResponseBreached),
                })
                .FirstOrDefaultAsync(cancellationToken) ?? new AgentDashboardTilesDto();

            queue = await AgentDashboardQueries.OrderedQueue(mine).Take(10).ToListAsync(cancellationToken);
        }

        if (canViewStats)
        {
            stats = await AgentDashboardQueries.PersonalAndTeamStatsAsync(db, currentUser, clock, cancellationToken);
        }

        return new AgentDashboardDto
        {
            CanViewQueue = canViewQueue,
            CanViewStats = canViewStats,
            Tiles = tiles,
            Queue = queue,
            Stats = stats,
        };
    }
}

/// <summary>Shared by <see cref="GetAgentDashboardQuery"/> and <see cref="GetAgentQueueQuery"/>, so the two can never disagree about ordering.</summary>
internal static class AgentDashboardQueries
{
    public static IQueryable<Ticket> MyOpenTickets(IAppDbContext db, ICurrentUser currentUser) =>
        db.Tickets.AsNoTracking()
            .WhereBranchAccessible(currentUser)
            .Where(t => t.AssignedAgentId == currentUser.UserId && !t.Status.IsTerminal);

    /// <summary>
    /// Breached first, then by remaining time (nulls last — see the <c>?? MaxValue</c>, which stops a
    /// raw null ordering from sorting a ticket with no SLA policy first on some providers), then
    /// priority level descending, then oldest.
    /// </summary>
    public static IQueryable<AgentQueueItemDto> OrderedQueue(IQueryable<Ticket> mine) =>
        mine
            .OrderByDescending(t => t.IsResolutionBreached || t.IsFirstResponseBreached)
            .ThenBy(t => t.ResolutionDueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(t => t.Priority.Level)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new AgentQueueItemDto
            {
                Id = t.Id,
                Number = t.Number,
                Subject = t.Subject,
                CustomerId = t.CustomerId,
                CustomerDisplayNameEn = t.Customer.DisplayName.En,
                CustomerDisplayNameAr = t.Customer.DisplayName.Ar,
                PriorityId = t.PriorityId,
                PriorityNameEn = t.Priority.Name.En,
                PriorityNameAr = t.Priority.Name.Ar,
                PriorityColorHex = t.Priority.ColorHex,
                ResolutionDueAt = t.ResolutionDueAt,
                IsFirstResponseBreached = t.IsFirstResponseBreached,
                IsResolutionBreached = t.IsResolutionBreached,
                CreatedAt = t.CreatedAt,
            });

    /// <summary>
    /// Resolved-this-week and the team average. `TicketDailyMetric` (CS-901) is the eventual source
    /// for both — until it's populated, this is a direct count, exactly as the story calls for.
    /// "Team" is approximated as every agent who resolved a ticket this week in the caller's own
    /// departments (falling back to the whole branch when the caller has none) — the closest
    /// meaningful grouping available without department/team-membership reporting, which does not
    /// exist yet. Never a per-agent ranking, per the story's own "context, not a leaderboard" rule.
    /// </summary>
    public static async Task<AgentDashboardStatsDto> PersonalAndTeamStatsAsync(
        IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, CancellationToken ct)
    {
        var startOfWeek = StartOfWeek(clock.UtcNow);

        var resolvedThisWeek = db.Tickets.AsNoTracking()
            .WhereBranchAccessible(currentUser)
            .Where(t => t.ResolvedById != null && t.ResolvedAt != null && t.ResolvedAt >= startOfWeek);

        if (currentUser.DepartmentIds.Count > 0)
        {
            var departments = currentUser.DepartmentIds.ToList();
            resolvedThisWeek = resolvedThisWeek.Where(t => t.DepartmentId != null && departments.Contains(t.DepartmentId.Value));
        }

        var myCount = await resolvedThisWeek.CountAsync(t => t.ResolvedById == currentUser.UserId, ct);

        var perAgentCounts = await resolvedThisWeek
            .GroupBy(t => t.ResolvedById)
            .Select(g => g.Count())
            .ToListAsync(ct);

        return new AgentDashboardStatsDto
        {
            ResolvedThisWeek = myCount,
            TeamAverageResolvedThisWeek = perAgentCounts.Count == 0 ? 0 : perAgentCounts.Average(),
        };
    }

    /// <summary>Monday 00:00 UTC of the current week — an ISO-8601 week start, not locale-specific.</summary>
    private static DateTimeOffset StartOfWeek(DateTimeOffset now)
    {
        var diff = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return new DateTimeOffset(now.UtcDateTime.Date.AddDays(-diff), TimeSpan.Zero);
    }
}
