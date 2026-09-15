using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Closes resolved tickets that have sat idle past <c>tickets.autoCloseResolvedAfterDays</c> with no
/// later customer reply. Runs hourly (see the trigger registered in <c>DependencyInjection</c>).
/// Processes in batches of 500 so one run never holds a transaction open over the whole table.
/// </summary>
[DisallowConcurrentExecution]
public class AutoCloseResolvedTicketsJob(
    AppDbContext db,
    IAutoCloseSettingsProvider settings,
    IDateTimeProvider clock,
    ITicketEventRecorder events,
    ILogger<AutoCloseResolvedTicketsJob> logger) : IJob
{
    private const int BatchSize = 500;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var days = await settings.GetAutoCloseResolvedAfterDaysAsync(ct);
        var cutoff = clock.UtcNow.AddDays(-days);

        var closedStatus = await db.TicketStatuses
            .FirstOrDefaultAsync(s => s.Kind == TicketStatusKind.Closed && s.IsActive, ct);

        if (closedStatus is null)
        {
            logger.LogWarning("Auto-close skipped: no active Closed-kind status is configured.");
            return;
        }

        // Resolved-kind statuses are a small, effectively-static set — loaded once rather than
        // re-queried per ticket in the loop below.
        var resolvedStatuses = await db.TicketStatuses
            .Where(s => s.Kind == TicketStatusKind.Resolved)
            .ToDictionaryAsync(s => s.Id, ct);

        var totalClosed = 0;

        while (true)
        {
            var batch = await db.Tickets
                .Where(t => t.Status.Kind == TicketStatusKind.Resolved
                    && t.ResolvedAt != null && t.ResolvedAt <= cutoff)
                .OrderBy(t => t.ResolvedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var ticket in batch)
            {
                var oldStatus = resolvedStatuses[ticket.StatusId];
                ticket.StatusId = closedStatus.Id;
                ticket.ClosedAt = clock.UtcNow;

                events.Record(ticket.Id, TicketEventType.Closed,
                    field: nameof(Ticket.StatusId),
                    oldValue: oldStatus.Id.ToString(), newValue: closedStatus.Id.ToString(),
                    oldDisplay: oldStatus.Name.En, newDisplay: closedStatus.Name.En,
                    triggeredByRule: "auto-close");
            }

            await db.SaveChangesAsync(ct);
            totalClosed += batch.Count;

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        if (totalClosed > 0)
        {
            logger.LogInformation("Auto-close job closed {Count} resolved ticket(s).", totalClosed);
        }
    }
}
