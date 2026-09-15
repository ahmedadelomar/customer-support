using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Workspace;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Reminders;

/// <summary>Dismisses a reminder — clears it from the recipient's persistent toast queue. Owner-only.</summary>
public record DismissReminderCommand(Guid Id) : IRequest;

public class DismissReminderCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<DismissReminderCommand>
{
    public async Task Handle(DismissReminderCommand request, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Reminder), request.Id);

        if (reminder.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the reminder's own recipient may dismiss it.");
        }

        reminder.IsDismissed = true;
        reminder.DismissedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
