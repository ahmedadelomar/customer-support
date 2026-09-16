using System.Text.Json;
using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Writes the in-app <see cref="Notification"/> row and queues external fan-out through the outbox
/// (SLA and Automation / Alerts and notifications). Adds to the caller's unit of work rather than
/// saving — the notification must commit with whatever triggered it, or a rolled-back transaction
/// would leave a notification for something that never happened.
/// </summary>
public class NotificationDispatcher(AppDbContext db, IDateTimeProvider clock) : INotificationDispatcher
{
    public async Task DispatchAsync(
        Guid userId,
        string eventType,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        string severity = "Info",
        CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user is null)
        {
            return; // never notify a deactivated (or nonexistent) account
        }

        var now = clock.UtcNow;

        // 1. The in-app row is always created, regardless of preferences or quiet hours.
        var notification = new Notification
        {
            UserId = userId,
            BranchId = user.BranchId,
            EventType = eventType,
            Title = new LocalizedText(titleEn, titleAr),
            Body = new LocalizedText(bodyEn, bodyAr),
            Link = link,
            Severity = severity,
            CreatedAt = now,
        };
        db.Notifications.Add(notification);

        // 2. External channels follow preferences, defaulting when no row exists.
        var pref = await db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.EventType == eventType, ct);

        var isCritical = string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase);
        var zone = ResolveZone(user.TimeZoneId);
        var inQuietHours = !isCritical && pref is not null && IsWithinQuietHours(pref, zone, now);

        var defaults = NotificationEventTypes.Find(eventType);
        var (viaEmail, viaSms, viaPush) = pref is not null
            ? (pref.ViaEmail, pref.ViaSms, pref.ViaPush)
            : (defaults?.DefaultViaEmail ?? false, defaults?.DefaultViaSms ?? false, defaults?.DefaultViaPush ?? false);

        var channels = new List<NotificationChannel>();
        if (viaEmail) channels.Add(NotificationChannel.Email);
        if (viaSms) channels.Add(NotificationChannel.Sms);
        if (viaPush) channels.Add(NotificationChannel.Push);

        foreach (var channel in channels)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = $"notification.{channel}".ToLowerInvariant(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    notificationId = notification.Id,
                    userId,
                    channel = channel.ToString(),
                    recipientEmail = user.Email,
                    recipientPhone = user.PhoneNumber,
                    language = user.PreferredLanguage,
                    title = user.PreferredLanguage == "ar" ? titleAr : titleEn,
                    body = user.PreferredLanguage == "ar" ? bodyAr : bodyEn,
                    eventType,
                    link,
                }),
                AggregateType = nameof(Notification),
                AggregateId = notification.Id,
                OccurredAt = now,
                // Quiet hours defer the send rather than dropping it. Critical severity bypasses this entirely.
                NextAttemptAt = inQuietHours ? QuietHoursEndAsUtc(pref!, zone, now) : now,
            });
        }

        notification.DispatchedChannels = channels.Count > 0 ? string.Join(',', channels) : null;
        // The caller saves, so the notification commits with whatever triggered it.
    }

    private static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static bool IsWithinQuietHours(NotificationPreference pref, TimeZoneInfo zone, DateTimeOffset nowUtc)
    {
        if (pref.QuietHoursStart is not { } start || pref.QuietHoursEnd is not { } end)
        {
            return false;
        }

        var localNow = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);

        // An overnight window (e.g. 22:00-06:00) wraps past midnight.
        return start <= end
            ? localNow >= start && localNow < end
            : localNow >= start || localNow < end;
    }

    /// <summary>The next UTC instant quiet hours end, used as the outbox row's deferred send time.</summary>
    private static DateTimeOffset QuietHoursEndAsUtc(NotificationPreference pref, TimeZoneInfo zone, DateTimeOffset nowUtc)
    {
        var end = pref.QuietHoursEnd!.Value;
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var candidateLocal = DateOnly.FromDateTime(localNow.DateTime).ToDateTime(end, DateTimeKind.Unspecified);
        var candidate = new DateTimeOffset(candidateLocal, zone.GetUtcOffset(candidateLocal));

        if (candidate <= nowUtc)
        {
            candidateLocal = candidateLocal.AddDays(1);
            candidate = new DateTimeOffset(candidateLocal, zone.GetUtcOffset(candidateLocal));
        }

        return candidate;
    }
}
