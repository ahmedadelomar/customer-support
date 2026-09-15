using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Workspace;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>Tasks linked to one ticket — the ticket screen's tasks panel.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetTicketTasksQuery(Guid TicketId) : IRequest<IReadOnlyList<AgentTaskDto>>;

public class GetTicketTasksQueryHandler(
    IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames, IDateTimeProvider clock)
    : IRequestHandler<GetTicketTasksQuery, IReadOnlyList<AgentTaskDto>>
{
    public async Task<IReadOnlyList<AgentTaskDto>> Handle(GetTicketTasksQuery request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.TicketId);
        }

        var rows = await db.AgentTasks.AsNoTracking()
            .Include(t => t.Reminders)
            .Where(t => t.TicketId == request.TicketId)
            .OrderBy(t => t.Status == AgentTaskStatus.Completed || t.Status == AgentTaskStatus.Cancelled)
            .ThenBy(t => t.DueAt ?? DateTimeOffset.MaxValue)
            .ToListAsync(cancellationToken);

        return await AgentTaskDtoMapper.MapAsync(rows, db, userNames, currentUser, clock, cancellationToken);
    }
}
