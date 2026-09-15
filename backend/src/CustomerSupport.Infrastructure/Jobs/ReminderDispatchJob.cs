using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Workspace;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Fires due reminders (Agent Dashboard / Tasks and reminders). Runs every minute (see the trigger
/// registered in <c>DependencyInjection</c>). Claims each row with a conditional
/// <c>ExecuteUpdateAsync</c> before dispatching — the update only succeeds for the instance that gets
/// there first, so an overlapping run (or, as a second layer, <see cref="DisallowConcurrentExecutionAttribute"/>
/// on this same job) can never send the same reminder twice. Marking sent BEFORE dispatching is
/// deliberate: a duplicate reminder is worse than a missed one, and a failed dispatch is still visible
/// in the notification log rather than silently retried forever.
/// </summary>
[DisallowConcurrentExecution]
public class ReminderDispatchJob(
    AppDbContext db,
    IDateTimeProvider clock,
    INotificationDispatcher dispatcher,
    ILogger<ReminderDispatchJob> logger) : IJob
{
    private const int BatchSize = 200;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = clock.UtcNow;

        var claimed = await db.Reminders
            .Where(r => !r.IsSent && !r.IsDismissed && r.RemindAt <= now)
            .OrderBy(r => r.RemindAt)
            .Take(BatchSize)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var dispatched = 0;

        foreach (var id in claimed)
        {
            var updated = await db.Reminders
                .Where(r => r.Id == id && !r.IsSent)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.IsSent, true)
                    .SetProperty(r => r.SentAt, now), ct);

            if (updated == 0)
            {
                continue; // another instance already claimed it
            }

            var reminder = await db.Reminders.AsNoTracking().FirstAsync(r => r.Id == id, ct);

            try
            {
                await dispatcher.DispatchAsync(
                    reminder.UserId,
                    "reminder.due",
                    "Reminder",
                    "تذكير",
                    reminder.Message,
                    reminder.Message,
                    link: reminder.TicketId is { } ticketId ? $"/agent/tickets/{ticketId}" : "/agent/tasks",
                    ct: ct);

                await db.SaveChangesAsync(ct);
                dispatched++;
            }
            catch (Exception ex)
            {
                // The reminder is already marked sent — that is deliberate (see the class remark).
                // A failed dispatch is a delivery problem to fix, not a reason to resend.
                logger.LogError(ex, "Failed to dispatch reminder {ReminderId} to user {UserId}.", id, reminder.UserId);
            }
        }

        if (dispatched > 0)
        {
            logger.LogInformation("Reminder dispatch job sent {Count} reminder(s).", dispatched);
        }
    }
}
