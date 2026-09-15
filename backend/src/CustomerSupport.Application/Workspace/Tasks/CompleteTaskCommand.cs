using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>
/// Marks a task complete. Open to the task's own assignee or anyone holding
/// <c>workspace.tasks.manage</c> — deliberately no <c>[RequirePermission]</c> attribute, since the
/// "owner OR manage" rule can't be expressed as one permission; the handler checks it explicitly.
/// Cancels any unsent reminders on the task, since a reminder for finished work is pure noise.
/// </summary>
public record CompleteTaskCommand(Guid Id) : IRequest;

public class CompleteTaskCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<CompleteTaskCommand>
{
    public async Task Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.AgentTask), request.Id);

        if (task.AssignedToId != currentUser.UserId && !currentUser.HasPermission(Permissions.Workspace.ManageTasks))
        {
            throw new ForbiddenException("Only the task's assignee or a workspace manager may complete it.");
        }

        await AgentTaskCompletion.CompleteAsync(task, currentUser.UserId, db, clock, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
