using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Delivers queued external notifications (SLA and Automation / Alerts and notifications). Runs
/// every 30 seconds over the <c>IX_OutboxMessage_Dispatch</c> index, oldest first, in batches of 100.
/// Scoped to <c>notification.*</c> rows — the other outbox consumers (webhooks, ERP sync) belong to
/// their own features once those are built.
/// </summary>
[DisallowConcurrentExecution]
public class OutboxDispatcherJob(
    AppDbContext db,
    IExternalNotificationSender sender,
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

    /// <summary>More than this many pending same-type events for one user within the window collapse into one.</summary>
    private const int DigestThreshold = 5;
    private static readonly TimeSpan DigestWindow = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record Payload(
        Guid NotificationId, Guid UserId, string Channel, string? RecipientEmail, string? RecipientPhone,
        string Language, string Title, string Body, string EventType, string? Link);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = clock.UtcNow;

        await CollapseDigestsAsync(now, ct);

        var due = await db.OutboxMessages
            .Where(o => o.ProcessedAt == null && o.Type.StartsWith("notification.")
                && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
            .OrderBy(o => o.OccurredAt)
            .Take(BatchSize)
            .Select(o => new { o.Id, o.NextAttemptAt })
            .ToListAsync(ct);

        var sent = 0;
        var abandoned = 0;

        foreach (var row in due)
        {
            // Claim by pushing the due time forward, conditioned on it still matching what we just
            // read — exactly the compare-and-swap the reminder job uses via ExecuteUpdateAsync, so an
            // overlapping run (another instance, since DisallowConcurrentExecution only covers this
            // one) can never send the same message twice.
            var claimed = await db.OutboxMessages
                .Where(o => o.Id == row.Id && o.ProcessedAt == null && o.NextAttemptAt == row.NextAttemptAt)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.NextAttemptAt, now.AddMinutes(2)), ct);

            if (claimed == 0)
            {
                continue; // another instance claimed it first
            }

            var message = await db.OutboxMessages.FirstAsync(o => o.Id == row.Id, ct);

            Payload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<Payload>(message.PayloadJson, JsonOptions);
            }
            catch (JsonException)
            {
                payload = null;
            }

            if (payload is null || !Enum.TryParse<NotificationChannel>(payload.Channel, true, out var channel))
            {
                message.ProcessedAt = now;
                message.Error = "Malformed payload — abandoned without sending.";
                await db.SaveChangesAsync(ct);
                continue;
            }

            try
            {
                await sender.SendAsync(channel, payload.RecipientEmail, payload.RecipientPhone, payload.Title, payload.Body, payload.Link, ct);
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

    /// <summary>
    /// Collapses a burst of same-type, same-user pending notifications into one. Rewrites the
    /// earliest row's payload into a summary and marks the rest processed (merged), so they are
    /// skipped by the claim loop above rather than each sent individually.
    /// </summary>
    private async Task CollapseDigestsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var pending = await db.OutboxMessages
            .Where(o => o.ProcessedAt == null && o.Type.StartsWith("notification.") && o.OccurredAt >= now - DigestWindow)
            .ToListAsync(ct);

        if (pending.Count <= DigestThreshold)
        {
            return; // cheapest possible exit for the common case (no burst at all)
        }

        var parsed = pending
            .Select(o => (Row: o, Payload: TryParse(o.PayloadJson)))
            .Where(x => x.Payload is not null)
            .ToList();

        var changed = false;

        foreach (var group in parsed.GroupBy(x => (x.Payload!.UserId, x.Payload.EventType, x.Row.Type)))
        {
            var rows = group.OrderBy(x => x.Row.OccurredAt).ToList();
            if (rows.Count <= DigestThreshold)
            {
                continue;
            }

            var keep = rows[0];
            foreach (var extra in rows.Skip(1))
            {
                extra.Row.ProcessedAt = now;
                extra.Row.Error = "Merged into a digest notification.";
            }

            var digestPayload = keep.Payload! with
            {
                Title = $"{rows.Count} new {group.Key.EventType} notifications",
                Body = $"You have {rows.Count} new notifications you haven't seen yet.",
                Link = "/agent/dashboard",
            };
            keep.Row.PayloadJson = JsonSerializer.Serialize(digestPayload);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static Payload? TryParse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Payload>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
