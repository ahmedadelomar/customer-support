using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>Cache key convention shared with the calendar admin commands, which invalidate on write.</summary>
public static class BusinessCalendarCacheKeys
{
    public static string For(Guid calendarId) => $"sla:calendar:{calendarId}";
}

/// <summary>The write side of the calendar cache — see <see cref="IBusinessCalendarCacheInvalidator"/>.</summary>
public class BusinessCalendarCacheInvalidator(IMemoryCache cache) : IBusinessCalendarCacheInvalidator
{
    public void Invalidate(Guid calendarId) => cache.Remove(BusinessCalendarCacheKeys.For(calendarId));
}

/// <summary>Immutable snapshot of one calendar's hours and holidays, as loaded into the cache.</summary>
internal sealed record CachedCalendar(
    bool IsTwentyFourSeven,
    string TimeZoneId,
    IReadOnlyList<(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime)> BusinessHours,
    IReadOnlyList<(DateOnly Date, bool IsRecurringAnnually)> Holidays);

/// <summary>
/// Working-hours arithmetic behind every SLA calculation (SLA and Automation / Response and
/// resolution targets). Runs on every ticket create, status change and breach-sweep tick, so
/// calendars are cached aggressively — see <see cref="BusinessCalendarCacheKeys"/> for how the
/// calendar admin endpoints invalidate an entry the moment it changes.
/// </summary>
public class BusinessCalendarCalculator(AppDbContext db, IMemoryCache cache) : IBusinessCalendarCalculator
{
    /// <summary>
    /// Ten thousand days (~27 years) of consecutive non-working days is not a real calendar
    /// misconfiguration to tolerate — it is the infinite loop the story asks to guard against.
    /// </summary>
    private const int MaxDaysScanned = 10_000;

    public async Task<DateTimeOffset> AddWorkingMinutesAsync(
        Guid calendarId, DateTimeOffset from, int minutes, CancellationToken ct = default)
    {
        if (minutes <= 0)
        {
            return from;
        }

        var calendar = await LoadCachedAsync(calendarId, ct);
        if (calendar.IsTwentyFourSeven)
        {
            return from.AddMinutes(minutes);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var cursor = TimeZoneInfo.ConvertTime(from, zone);
        var remaining = minutes;

        foreach (var (start, end) in WorkingSegments(calendar, cursor, zone))
        {
            var available = (int)(end - start).TotalMinutes;
            if (available >= remaining)
            {
                return start.AddMinutes(remaining);
            }

            remaining -= available;
        }

        // Unreachable in practice: WorkingSegments either yields forever or throws once it has
        // scanned MaxDaysScanned days with no working hours at all.
        throw new InvalidOperationException($"Calendar {calendarId} could not satisfy {minutes} working minutes.");
    }

    public async Task<int> WorkingMinutesBetweenAsync(
        Guid calendarId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        if (to <= from)
        {
            return 0;
        }

        var calendar = await LoadCachedAsync(calendarId, ct);
        if (calendar.IsTwentyFourSeven)
        {
            return (int)(to - from).TotalMinutes;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var cursor = TimeZoneInfo.ConvertTime(from, zone);
        var total = 0;

        foreach (var (start, end) in WorkingSegments(calendar, cursor, zone))
        {
            if (start >= to)
            {
                break;
            }

            var segmentEnd = end < to ? end : to;
            if (segmentEnd > start)
            {
                total += (int)(segmentEnd - start).TotalMinutes;
            }

            if (end >= to)
            {
                break;
            }
        }

        return total;
    }

    /// <summary>
    /// Lazily yields every working window from <paramref name="fromLocal"/> onward, clipped so the
    /// first window never starts before it. Both public methods walk the same sequence — one
    /// consuming it to add minutes, the other summing overlap with an end instant — so the calendar
    /// (and its DST handling) is implemented exactly once.
    /// </summary>
    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> WorkingSegments(
        CachedCalendar calendar, DateTimeOffset fromLocal, TimeZoneInfo zone)
    {
        var cursor = fromLocal;

        for (var daysScanned = 0; daysScanned < MaxDaysScanned; daysScanned++)
        {
            var day = DateOnly.FromDateTime(cursor.DateTime);

            if (!IsHoliday(calendar, day))
            {
                var windows = calendar.BusinessHours
                    .Where(h => h.DayOfWeek == cursor.DayOfWeek)
                    .OrderBy(h => h.StartTime);

                foreach (var window in windows)
                {
                    var open = OnDate(day, window.StartTime, zone);
                    var close = OnDate(day, window.EndTime, zone);

                    if (close <= cursor)
                    {
                        continue; // window already fully behind the cursor
                    }

                    var start = cursor < open ? open : cursor;
                    if (close > start)
                    {
                        yield return (start, close);
                    }
                }
            }

            cursor = StartOfNextDay(cursor, zone);
        }

        throw new InvalidOperationException(
            "Business calendar has no working hours at all — scanned "
            + $"{MaxDaysScanned} consecutive days without one, which would otherwise loop forever.");
    }

    private static bool IsHoliday(CachedCalendar calendar, DateOnly day) =>
        calendar.Holidays.Any(h =>
            h.Date == day || (h.IsRecurringAnnually && h.Date.Month == day.Month && h.Date.Day == day.Day));

    private static DateTimeOffset OnDate(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static DateTimeOffset StartOfNextDay(DateTimeOffset cursor, TimeZoneInfo zone) =>
        OnDate(DateOnly.FromDateTime(cursor.DateTime).AddDays(1), new TimeOnly(0, 0), zone);

    private async Task<CachedCalendar> LoadCachedAsync(Guid calendarId, CancellationToken ct)
    {
        if (cache.TryGetValue(BusinessCalendarCacheKeys.For(calendarId), out CachedCalendar? cached) && cached is not null)
        {
            return cached;
        }

        var calendar = await db.BusinessCalendars
            .Include(c => c.BusinessHours)
            .Include(c => c.Holidays)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calendarId, ct)
            ?? throw new InvalidOperationException($"Business calendar {calendarId} does not exist.");

        var snapshot = new CachedCalendar(
            calendar.IsTwentyFourSeven,
            calendar.TimeZoneId,
            calendar.BusinessHours.Select(h => (h.DayOfWeek, h.StartTime, h.EndTime)).ToList(),
            calendar.Holidays.Select(h => (h.Date, h.IsRecurringAnnually)).ToList());

        cache.Set(BusinessCalendarCacheKeys.For(calendarId), snapshot, TimeSpan.FromMinutes(30));
        return snapshot;
    }
}
