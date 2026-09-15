using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>Edits a task's own fields. Ticket/customer links are set only at creation. Reassigning notifies the new assignee.</summary>
[RequirePermission(Permissions.Workspace.ManageTasks)]
public record UpdateTaskCommand : IRequest
{
    public Guid Id { get; init; }
    public Guid AssignedToId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? PriorityId { get; init; }
    public DateTimeOffset? DueAt { get; init; }
}

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AssignedToId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public class UpdateTaskCommandHandler(IAppDbContext db, ICurrentUser currentUser, INotificationDispatcher notifications)
    : IRequestHandler<UpdateTaskCommand>
{
    public async Task Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.AgentTask), request.Id);

        var reassigned = task.AssignedToId != request.AssignedToId;

        task.AssignedToId = request.AssignedToId;
        task.Title = request.Title;
        task.Description = request.Description;
        task.PriorityId = request.PriorityId;
        task.DueAt = request.DueAt;

        await db.SaveChangesAsync(cancellationToken);

        if (reassigned && request.AssignedToId != currentUser.UserId)
        {
            await notifications.DispatchAsync(
                request.AssignedToId,
                "task.assigned",
                "You've been assigned a task",
                "تم إسناد مهمة إليك",
                $"Task: {request.Title}",
                $"المهمة: {request.Title}",
                link: task.TicketId is { } ticketId ? $"/agent/tickets/{ticketId}" : "/agent/tasks",
                ct: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
