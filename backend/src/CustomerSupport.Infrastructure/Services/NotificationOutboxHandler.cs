using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Delivers <c>notification.*</c> outbox rows (SLA and Automation / Alerts and notifications) —
/// the per-row send and the digest-collapsing pre-pass that used to live inline in the outbox job
/// before <see cref="Jobs.OutboxDispatcherJob"/> became generic (CS-301).
/// </summary>
public class NotificationOutboxHandler(
    AppDbContext db, IExternalNotificationSender sender) : IOutboxMessageHandler
{
    private const string TypePrefix = "notification.";

    /// <summary>More than this many pending same-type events for one user within the window collapse into one.</summary>
    private const int DigestThreshold = 5;
    private static readonly TimeSpan DigestWindow = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record Payload(
        Guid NotificationId, Guid UserId, string Channel, string? RecipientEmail, string? RecipientPhone,
        string Language, string Title, string Body, string EventType, string? Link);

    public bool CanHandle(string type) => type.StartsWith(TypePrefix, StringComparison.Ordinal);

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<Payload>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Malformed notification outbox payload.");

        if (!Enum.TryParse<NotificationChannel>(payload.Channel, true, out var channel))
        {
            throw new InvalidOperationException($"Unknown notification channel '{payload.Channel}'.");
        }

        await sender.SendAsync(channel, payload.RecipientEmail, payload.RecipientPhone, payload.Title, payload.Body, payload.Link, ct);
    }

    /// <summary>
    /// Collapses a burst of same-type, same-user pending notifications into one. Rewrites the
    /// earliest row's payload into a summary and marks the rest processed (merged), so they are
    /// skipped by the job's claim loop rather than each sent individually.
    /// </summary>
    public async Task CollapseAsync(DateTimeOffset now, CancellationToken ct)
    {
        var pending = await db.OutboxMessages
            .Where(o => o.ProcessedAt == null && o.Type.StartsWith(TypePrefix) && o.OccurredAt >= now - DigestWindow)
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
