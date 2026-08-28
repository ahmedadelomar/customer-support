using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Sla;

/// <summary>One working window on one weekday. Multiple rows per day allow a split shift.</summary>
public class BusinessHour : BaseEntity
{
    public Guid BusinessCalendarId { get; set; }
    public BusinessCalendar BusinessCalendar { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
