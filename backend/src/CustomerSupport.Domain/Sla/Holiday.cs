using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Sla;

/// <summary>A non-working date excluded from SLA elapsed-time calculations.</summary>
public class Holiday : BaseEntity
{
    public Guid BusinessCalendarId { get; set; }
    public BusinessCalendar BusinessCalendar { get; set; } = null!;

    public DateOnly Date { get; set; }
    public LocalizedText Name { get; set; } = new();
    /// <summary>True for fixed-date holidays that repeat every year.</summary>
    public bool IsRecurringAnnually { get; set; }
}
