using CustomerSupport.Application.Channels.WebForms;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Retries submissions still <c>Failed</c> (Communication Channels / Web forms, CS-305). The entity
/// carries no attempt counter or backoff schedule — retrying a genuine configuration problem (a
/// deactivated category, say) is harmless and idempotent, so every run simply re-attempts every
/// still-failed row; once an administrator fixes the cause, the next run clears it.
/// </summary>
[DisallowConcurrentExecution]
public class WebFormSubmissionRetryJob(
    AppDbContext db, IWebFormSubmissionRetryService retry, ILogger<WebFormSubmissionRetryJob> logger) : IJob
{
    private const int BatchSize = 50;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var failed = await db.WebFormSubmissions
            .Where(s => s.Status == "Failed")
            .OrderBy(s => s.SubmittedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (failed.Count == 0)
        {
            return;
        }

        var recovered = 0;
        foreach (var submission in failed)
        {
            if (await retry.RetryAsync(submission, ct) is not null)
            {
                recovered++;
            }
        }

        logger.LogInformation("Web form retry sweep: {Recovered}/{Total} recovered.", recovered, failed.Count);
    }
}
