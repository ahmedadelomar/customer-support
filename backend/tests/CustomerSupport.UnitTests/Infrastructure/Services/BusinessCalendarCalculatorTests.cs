using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Sla;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace CustomerSupport.UnitTests.Infrastructure.Services;

/// <summary>
/// The arithmetic underneath every SLA due date (CS-501). Written before the engine that calls it,
/// per the story's own instruction — this is not something to verify by clicking through a UI.
/// Uses a real Sqlite connection (not the InMemory provider) only because <see cref="AppDbContext"/>
/// requires a relational provider to be configured at all; no relational behaviour is under test here.
/// </summary>
public class BusinessCalendarCalculatorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private BusinessCalendarCalculator _calculator = null!;
    private Guid _calendarId;
    private Guid _splitShiftCalendarId;
    private Guid _twentyFourSevenCalendarId;
    private Guid _dstCalendarId;

    private static readonly TimeZoneInfo Riyadh = TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _calculator = new BusinessCalendarCalculator(_db, new MemoryCache(new MemoryCacheOptions()));

        // Sunday-Thursday 08:00-17:00 Asia/Riyadh, matching the seeded default calendar.
        var calendar = new BusinessCalendar
        {
            Name = new LocalizedText("Standard", "قياسي"),
            TimeZoneId = "Asia/Riyadh",
        };
        foreach (var day in new[]
                 {
                     DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
                     DayOfWeek.Wednesday, DayOfWeek.Thursday,
                 })
        {
            calendar.BusinessHours.Add(new BusinessHour { DayOfWeek = day, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(17, 0) });
        }
        calendar.Holidays.Add(new Holiday { Date = new DateOnly(2026, 9, 27), Name = new LocalizedText("Test Holiday", "عطلة اختبار") });
        calendar.Holidays.Add(new Holiday { Date = new DateOnly(2020, 9, 23), IsRecurringAnnually = true, Name = new LocalizedText("National Day", "اليوم الوطني") });
        _db.BusinessCalendars.Add(calendar);

        var splitShift = new BusinessCalendar { Name = new LocalizedText("Split", "منقسم"), TimeZoneId = "Asia/Riyadh" };
        splitShift.BusinessHours.Add(new BusinessHour { DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) });
        splitShift.BusinessHours.Add(new BusinessHour { DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(18, 0) });
        _db.BusinessCalendars.Add(splitShift);

        var alwaysOn = new BusinessCalendar { Name = new LocalizedText("24/7", "24/7"), TimeZoneId = "UTC", IsTwentyFourSeven = true };
        _db.BusinessCalendars.Add(alwaysOn);

        // A calendar in a DST-observing zone, open every day, to exercise the transition.
        var dstCalendar = new BusinessCalendar { Name = new LocalizedText("DST", "DST"), TimeZoneId = "America/New_York" };
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            dstCalendar.BusinessHours.Add(new BusinessHour { DayOfWeek = day, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59) });
        }
        _db.BusinessCalendars.Add(dstCalendar);

        await _db.SaveChangesAsync();

        _calendarId = calendar.Id;
        _splitShiftCalendarId = splitShift.Id;
        _twentyFourSevenCalendarId = alwaysOn.Id;
        _dstCalendarId = dstCalendar.Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>A known Sunday within the seeded calendar's coverage, in Asia/Riyadh local time.</summary>
    private static DateTimeOffset Riyadh2026(int month, int day, int hour, int minute = 0) =>
        new DateTimeOffset(new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified), Riyadh.GetUtcOffset(new DateTime(2026, month, day, hour, minute, 0)));

    [Fact]
    public async Task WithinOneDay_AddsDirectly()
    {
        // Sunday 2026-09-13.
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 13, 9, 0), 240);
        result.Should().Be(Riyadh2026(9, 13, 13, 0));
    }

    [Fact]
    public async Task SpanningAClose_RollsToNextMorning()
    {
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 13, 16, 0), 120);
        result.Should().Be(Riyadh2026(9, 14, 9, 0));
    }

    [Fact]
    public async Task SpanningAWeekend_SkipsFridayAndSaturday()
    {
        // Thursday 2026-09-17 16:00 + 240 min = Sunday 2026-09-20 11:00.
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 17, 16, 0), 240);
        result.Should().Be(Riyadh2026(9, 20, 11, 0));
    }

    [Fact]
    public async Task ArrivingBeforeOpening_StartsAtOpen()
    {
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 13, 6, 0), 60);
        result.Should().Be(Riyadh2026(9, 13, 9, 0));
    }

    [Fact]
    public async Task ArrivingAfterClosing_RollsToNextMorning()
    {
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 13, 19, 0), 60);
        result.Should().Be(Riyadh2026(9, 14, 9, 0));
    }

    [Fact]
    public async Task ArrivingOnANonWorkingDay_RollsToNextSunday()
    {
        // Friday 2026-09-18 is not a working day for this calendar.
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 18, 10, 0), 60);
        result.Should().Be(Riyadh2026(9, 20, 9, 0));
    }

    [Fact]
    public async Task Holiday_IsExcluded()
    {
        // 2026-09-27 (Sunday) is a one-off holiday on the seeded calendar. Thursday 2026-09-24 16:00
        // + 240 min would otherwise land on that Sunday at 12:00; with the holiday it rolls to Monday.
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 24, 16, 0), 240);
        result.Should().Be(Riyadh2026(9, 28, 11, 0));
    }

    [Fact]
    public async Task RecurringAnnualHoliday_IsExcludedEveryYear()
    {
        // National Day is seeded for 2020-09-23 but recurs annually — 2026-09-23 (Wednesday) must
        // also be skipped even though the seeded row is for a different year. Tuesday 2026-09-22
        // 16:30 + 60 min: 30 min closes Tuesday, Wednesday is skipped, the remaining 30 min lands
        // Thursday 2026-09-24 at 08:30.
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, Riyadh2026(9, 22, 16, 30), 60);
        result.Should().Be(Riyadh2026(9, 24, 8, 30));
    }

    [Fact]
    public async Task SplitShift_ConsumesBothWindowsInSequence()
    {
        // Sunday 11:00 + 120 min: 1h to close the morning window (11:00-12:00), then 1h into the
        // afternoon window starting 14:00 -> 15:00.
        var result = await _calculator.AddWorkingMinutesAsync(_splitShiftCalendarId, Riyadh2026(9, 13, 11, 0), 120);
        result.Should().Be(Riyadh2026(9, 13, 15, 0));
    }

    [Fact]
    public async Task TwentyFourSeven_AddsWallClockMinutes()
    {
        var from = new DateTimeOffset(2026, 9, 13, 23, 0, 0, TimeSpan.Zero);
        var result = await _calculator.AddWorkingMinutesAsync(_twentyFourSevenCalendarId, from, 180);
        result.Should().Be(from.AddMinutes(180));
    }

    [Fact]
    public async Task ZeroMinutes_ReturnsInputUnchanged()
    {
        var from = Riyadh2026(9, 13, 10, 0);
        var result = await _calculator.AddWorkingMinutesAsync(_calendarId, from, 0);
        result.Should().Be(from);
    }

    [Fact]
    public async Task DaylightSavingTransition_IsHandledByTheZone()
    {
        // US DST ended 2026-11-01 at 02:00 (clocks fall back). A calendar open all day in that zone
        // must still add wall-clock minutes correctly across the transition, matching what the .NET
        // time zone database itself reports for the elapsed real time.
        var from = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeZoneInfo.FindSystemTimeZoneById("America/New_York").GetUtcOffset(new DateTime(2026, 10, 31, 20, 0, 0)));
        var result = await _calculator.AddWorkingMinutesAsync(_dstCalendarId, from, 600); // 10 hours of working time
        var workingMinutesBack = await _calculator.WorkingMinutesBetweenAsync(_dstCalendarId, from, result);
        workingMinutesBack.Should().Be(600);
    }

    [Fact]
    public async Task WorkingMinutesBetween_IsTheInverseOfAddWorkingMinutes()
    {
        var cases = new[]
        {
            (Riyadh2026(9, 13, 9, 0), 240),
            (Riyadh2026(9, 13, 16, 0), 120),
            (Riyadh2026(9, 17, 16, 0), 240),
            (Riyadh2026(9, 13, 6, 0), 60),
            (Riyadh2026(9, 13, 19, 0), 60),
            (Riyadh2026(9, 18, 10, 0), 60),
        };

        foreach (var (from, minutes) in cases)
        {
            var due = await _calculator.AddWorkingMinutesAsync(_calendarId, from, minutes);
            var elapsed = await _calculator.WorkingMinutesBetweenAsync(_calendarId, from, due);
            elapsed.Should().Be(minutes, because: $"from {from} adding {minutes} minutes must invert exactly");
        }
    }

    [Fact]
    public async Task WorkingMinutesBetween_OnTwentyFourSeven_IsWallClock()
    {
        var from = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(5);
        var minutes = await _calculator.WorkingMinutesBetweenAsync(_twentyFourSevenCalendarId, from, to);
        minutes.Should().Be(300);
    }

    [Fact]
    public async Task NoWorkingHoursAtAll_ThrowsRatherThanLoopingForever()
    {
        var emptyCalendar = new BusinessCalendar { Name = new LocalizedText("Empty", "فارغ"), TimeZoneId = "UTC" };
        _db.BusinessCalendars.Add(emptyCalendar);
        await _db.SaveChangesAsync();

        var act = () => _calculator.AddWorkingMinutesAsync(emptyCalendar.Id, DateTimeOffset.UtcNow, 60);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
