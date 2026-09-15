using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Workspace;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>Input for the optional reminder a task can be created with, in the agent's own wall-clock time.</summary>
public record CreateTaskReminderInput
{
    /// <summary>Naive local date-time — no offset, interpreted against the caller's own time zone.</summary>
    public DateTime RemindAtLocal { get; init; }
    public IReadOnlyList<NotificationChannel> Channels { get; init; } = [NotificationChannel.InApp];
}

/// <summary>
/// Creates a task, optionally with one reminder in the same call — the compact composer's "optional
/// reminder with channel checkboxes". Assigning to someone other than the caller notifies them.
/// </summary>
[RequirePermission(Permissions.Workspace.ManageTasks)]
public record CreateTaskCommand : IRequest<Guid>
{
    public Guid? TicketId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid AssignedToId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? PriorityId { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public CreateTaskReminderInput? Reminder { get; init; }
}

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.AssignedToId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public class CreateTaskCommandHandler(
    IAppDbContext db, ICurrentUser currentUser, INotificationDispatcher notifications, IDateTimeProvider clock)
    : IRequestHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new AgentTask
        {
            BranchId = currentUser.BranchId,
            TicketId = request.TicketId,
            CustomerId = request.CustomerId,
            AssignedToId = request.AssignedToId,
            Title = request.Title,
            Description = request.Description,
            PriorityId = request.PriorityId,
            DueAt = request.DueAt,
        };

        db.AgentTasks.Add(task);

        if (request.Reminder is { } reminder)
        {
            var remindAtUtc = await ReminderScheduling.ToUtcAsync(reminder.RemindAtLocal, db, currentUser, cancellationToken);

            if (remindAtUtc <= clock.UtcNow)
            {
                throw new Common.Exceptions.ValidationException(new Dictionary<string, string[]>
                {
                    ["remindAt"] = ["The reminder time must be in the future."],
                });
            }

            db.Reminders.Add(new Reminder
            {
                AgentTaskId = task.Id,
                TicketId = request.TicketId,
                UserId = request.AssignedToId,
                Message = request.Title,
                RemindAt = remindAtUtc,
                Channels = AgentTaskDtoMapper.FormatChannels(reminder.Channels),
                CreatedAt = clock.UtcNow,
                CreatedById = currentUser.UserId,
            });
        }

        if (request.AssignedToId != currentUser.UserId)
        {
            await notifications.DispatchAsync(
                request.AssignedToId,
                "task.assigned",
                "You've been assigned a task",
                "تم إسناد مهمة إليك",
                $"Task: {request.Title}",
                $"المهمة: {request.Title}",
                link: request.TicketId is { } ticketId ? $"/agent/tickets/{ticketId}" : "/agent/tasks",
                ct: cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return task.Id;
    }
}

/// <summary>
/// The one place a naive local reminder time is converted to a UTC instant, so the create and
/// resolve-conflict paths can never disagree about what "the agent's own zone" means.
/// </summary>
internal static class ReminderScheduling
{
    public static async Task<DateTimeOffset> ToUtcAsync(
        DateTime remindAtLocal, IAppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var zoneId = currentUser.TimeZoneId;

        if (string.IsNullOrWhiteSpace(zoneId) && currentUser.BranchId is { } branchId)
        {
            zoneId = await db.Branches.AsNoTracking()
                .Where(b => b.Id == branchId)
                .Select(b => b.TimeZoneId)
                .FirstOrDefaultAsync(ct);
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId ?? "Asia/Riyadh");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh");
        }

        var naive = DateTime.SpecifyKind(remindAtLocal, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(naive, zone);

        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
