using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Workspace;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>
/// Paged task list (Agent Dashboard / Tasks and reminders). Defaults to the caller's own tasks;
/// <see cref="AssignedToId"/> is honoured only when the caller holds <c>workspace.tasks.assign</c> —
/// otherwise silently ignored, so an agent without it cannot enumerate a colleague's workload by
/// simply passing the parameter.
/// </summary>
[RequirePermission(Permissions.Workspace.ViewOwnTasks)]
public record GetTasksQuery : IRequest<PagedResult<AgentTaskDto>>
{
    public Guid? AssignedToId { get; init; }
    public AgentTaskStatus? Status { get; init; }
    public DateTimeOffset? DueFrom { get; init; }
    public DateTimeOffset? DueTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetTasksQueryValidator : AbstractValidator<GetTasksQuery>
{
    public GetTasksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public class GetTasksQueryHandler(
    IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames, IDateTimeProvider clock)
    : IRequestHandler<GetTasksQuery, PagedResult<AgentTaskDto>>
{
    public async Task<PagedResult<AgentTaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        var assignedToId = request.AssignedToId is { } requested && currentUser.HasPermission(Permissions.Workspace.AssignTasks)
            ? requested
            : currentUser.UserId!.Value;

        var query = db.AgentTasks.AsNoTracking().Where(t => t.AssignedToId == assignedToId);

        if (request.Status is { } status) query = query.Where(t => t.Status == status);
        if (request.DueFrom is { } from) query = query.Where(t => t.DueAt != null && t.DueAt >= from);
        if (request.DueTo is { } to) query = query.Where(t => t.DueAt != null && t.DueAt <= to);

        var totalCount = await query.CountAsync(cancellationToken);

        var now = clock.UtcNow;

        // Overdue first, then by due date (nulls last), then priority level descending.
        var rows = await query
            .Include(t => t.Reminders)
            .OrderByDescending(t => t.DueAt != null && t.DueAt < now
                && t.Status != AgentTaskStatus.Completed && t.Status != AgentTaskStatus.Cancelled)
            .ThenBy(t => t.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(t => t.PriorityId == null ? -1 : db.TicketPriorities.Where(p => p.Id == t.PriorityId).Select(p => p.Level).FirstOrDefault())
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = await AgentTaskDtoMapper.MapAsync(rows, db, userNames, currentUser, clock, cancellationToken);

        return PagedResult<AgentTaskDto>.Create(items, request.Page, request.PageSize, totalCount);
    }
}
