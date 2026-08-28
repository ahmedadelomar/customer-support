using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Sla;

/// <summary>
/// Working-hours definition an SLA policy measures against, so a target of 4 hours means 4 working
/// hours rather than 4 wall-clock hours.
/// </summary>
public class BusinessCalendar : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public LocalizedText Name { get; set; } = new();
    /// <summary>IANA time zone id, for example <c>Asia/Riyadh</c>.</summary>
    public string TimeZoneId { get; set; } = "Asia/Riyadh";
    /// <summary>When true, SLA clocks run continuously and the hour rows are ignored.</summary>
    public bool IsTwentyFourSeven { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<BusinessHour> BusinessHours { get; set; } = new List<BusinessHour>();
    public ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
