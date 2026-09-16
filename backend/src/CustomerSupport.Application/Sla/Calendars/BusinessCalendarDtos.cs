namespace CustomerSupport.Application.Sla.Calendars;

public record BusinessHourDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public record HolidayDto(DateOnly Date, string NameEn, string NameAr, bool IsRecurringAnnually);

public record BusinessHourInput(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public record HolidayInput(DateOnly Date, string NameEn, string NameAr, bool IsRecurringAnnually);

public record BusinessCalendarDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = string.Empty;
    public bool IsTwentyFourSeven { get; init; }
    public bool IsDefault { get; init; }
    public bool IsInUse { get; init; }
    public IReadOnlyList<BusinessHourDto> BusinessHours { get; init; } = [];
    public IReadOnlyList<HolidayDto> Holidays { get; init; } = [];
}
