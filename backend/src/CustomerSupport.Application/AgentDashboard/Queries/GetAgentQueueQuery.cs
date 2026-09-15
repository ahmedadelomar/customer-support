using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.AgentDashboard.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.AgentDashboard.Queries;

/// <summary>
/// Just the next-up queue, for the dashboard's auto-refresh poll — cheaper than re-fetching tiles and
/// stats on every interval. Same ordering as <see cref="GetAgentDashboardQuery"/>, via the shared
/// <see cref="AgentDashboardQueries"/> helper.
/// </summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetAgentQueueQuery : IRequest<IReadOnlyList<AgentQueueItemDto>>;

public class GetAgentQueueQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetAgentQueueQuery, IReadOnlyList<AgentQueueItemDto>>
{
    public async Task<IReadOnlyList<AgentQueueItemDto>> Handle(GetAgentQueueQuery request, CancellationToken cancellationToken)
    {
        var mine = AgentDashboardQueries.MyOpenTickets(db, currentUser);
        return await AgentDashboardQueries.OrderedQueue(mine).Take(10).ToListAsync(cancellationToken);
    }
}
