using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// The one generic outbox dispatcher every outbox producer shares — notifications (CS-504) and
/// customer-facing email replies (CS-301) today, WhatsApp/SMS (CS-302/304) next. Runs every 30
/// seconds over the <c>IX_OutboxMessage_Dispatch</c> index, oldest first, in batches of 100. Delivery
/// itself is delegated to whichever registered <see cref="IOutboxMessageHandler"/> claims a row's
/// <c>Type</c> — this job only owns claiming, backoff and abandonment, which is identical no matter
/// what is being sent.
/// </summary>
[DisallowConcurrentExecution]
public class OutboxDispatcherJob(
    AppDbContext db,
    IEnumerable<IOutboxMessageHandler> handlers,
    IDateTimeProvider clock,
    ILogger<OutboxDispatcherJob> logger) : IJob
{
    private const int BatchSize = 100;
    private const int MaxAttempts = 6;

    /// <summary>1m, 5m, 15m, 1h, 6h — indexed by the attempt number that just failed (1-based).</summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1), TimeSpan.FromHours(6),
    ];

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = clock.UtcNow;

        foreach (var handler in handlers)
        {
            await handler.CollapseAsync(now, ct);
        }

        var due = await db.OutboxMessages
            .Where(o => o.ProcessedAt == null && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
            .OrderBy(o => o.OccurredAt)
            .Take(BatchSize)
            .Select(o => new { o.Id, o.Type, o.NextAttemptAt })
            .ToListAsync(ct);

        var sent = 0;
        var abandoned = 0;

        foreach (var row in due)
        {
            // Claim by pushing the due time forward, conditioned on it still matching what we just
            // read — the same compare-and-swap the reminder job and round-robin assignment use, so
            // an overlapping run (another instance; DisallowConcurrentExecution only covers this one)
            // can never send the same message twice.
            var claimed = await db.OutboxMessages
                .Where(o => o.Id == row.Id && o.ProcessedAt == null && o.NextAttemptAt == row.NextAttemptAt)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.NextAttemptAt, now.AddMinutes(2)), ct);

            if (claimed == 0)
            {
                continue; // another instance claimed it first
            }

            var message = await db.OutboxMessages.FirstAsync(o => o.Id == row.Id, ct);
            var handler = handlers.FirstOrDefault(h => h.CanHandle(message.Type));

            if (handler is null)
            {
                message.ProcessedAt = now;
                message.Error = $"No handler registered for outbox type '{message.Type}'.";
                await db.SaveChangesAsync(ct);
                logger.LogError("Outbox message {Id} abandoned: no handler for type {Type}.", message.Id, message.Type);
                continue;
            }

            try
            {
                await handler.HandleAsync(message, ct);
                message.ProcessedAt = now;
                sent++;
            }
            catch (Exception ex)
            {
                message.Attempts += 1;
                message.Error = ex.Message;

                if (message.Attempts >= MaxAttempts)
                {
                    message.ProcessedAt = now; // abandoned — a failed send after 6 attempts is not retried forever
                    abandoned++;
                    logger.LogError(ex, "Outbox message {Id} abandoned after {Attempts} attempts.", message.Id, message.Attempts);
                }
                else
                {
                    var delay = Backoff[Math.Min(message.Attempts - 1, Backoff.Length - 1)];
                    message.NextAttemptAt = now.Add(delay);
                    logger.LogWarning(ex, "Outbox message {Id} failed (attempt {Attempt}); retrying in {Delay}.", message.Id, message.Attempts, delay);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        if (sent > 0 || abandoned > 0)
        {
            logger.LogInformation("Outbox dispatch: {Sent} sent, {Abandoned} abandoned.", sent, abandoned);
        }
    }
}
