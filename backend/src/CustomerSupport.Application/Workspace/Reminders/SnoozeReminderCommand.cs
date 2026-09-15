using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Workspace;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Reminders;

/// <summary>
/// Snoozes a reminder by creating a new one <c>Minutes</c> from now. The original's <c>IsSent</c>/
/// <c>SentAt</c> are never touched — that record that the first one fired is exactly what the story
/// asks to preserve — but it IS marked dismissed, the same as an explicit dismiss, so it stops
/// reappearing in the toast queue once the agent has acted on it. Owner-only: no
/// <c>[RequirePermission]</c>, since "owns this reminder" isn't a permission, it's a row-level check
/// the handler makes itself.
/// </summary>
public record SnoozeReminderCommand : IRequest<Guid>
{
    public Guid Id { get; init; }
    public int Minutes { get; init; }
}

public class SnoozeReminderCommandValidator : AbstractValidator<SnoozeReminderCommand>
{
    public SnoozeReminderCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Minutes).GreaterThan(0).LessThanOrEqualTo(60 * 24 * 7);
    }
}

public class SnoozeReminderCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<SnoozeReminderCommand, Guid>
{
    public async Task<Guid> Handle(SnoozeReminderCommand request, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Reminder), request.Id);

        if (reminder.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the reminder's own recipient may snooze it.");
        }

        var snoozed = new Reminder
        {
            AgentTaskId = reminder.AgentTaskId,
            TicketId = reminder.TicketId,
            UserId = reminder.UserId,
            Message = reminder.Message,
            RemindAt = clock.UtcNow.AddMinutes(request.Minutes),
            Channels = reminder.Channels,
            CreatedAt = clock.UtcNow,
            CreatedById = currentUser.UserId,
        };

        db.Reminders.Add(snoozed);

        reminder.IsDismissed = true;
        reminder.DismissedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return snoozed.Id;
    }
}
