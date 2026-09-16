using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Trims the audit trail to <c>audit.retentionDays</c> (Security &amp; Administration / Audit logs).
/// Runs nightly. Zero means keep forever.
/// </summary>
/// <remarks>
/// Deletes in batches: a single delete spanning months of rows holds a lock long enough to stall the
/// API, which is a poor trade for a housekeeping task. This is also the only place rows ever leave
/// the table — no API can modify or delete an audit entry, by design.
/// </remarks>
[DisallowConcurrentExecution]
public class AuditRetentionJob(
    AppDbContext db,
    IAuditRetentionSettings settings,
    IDateTimeProvider clock,
    ILogger<AuditRetentionJob> logger) : IJob
{
    private const int BatchSize = 5_000;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var retentionDays = await settings.GetRetentionDaysAsync(ct);

        if (retentionDays <= 0)
        {
            return;
        }

        var cutoff = clock.UtcNow.AddDays(-retentionDays);
        var total = 0;
        int deleted;

        do
        {
            deleted = await db.AuditLogs
                .Where(a => a.OccurredAt < cutoff)
                .Take(BatchSize)
                .ExecuteDeleteAsync(ct);

            total += deleted;
        }
        while (deleted == BatchSize && !ct.IsCancellationRequested);

        if (total > 0)
        {
            logger.LogInformation(
                "Audit retention removed {Count} entries older than {Cutoff:u}.", total, cutoff);
        }
    }
}
