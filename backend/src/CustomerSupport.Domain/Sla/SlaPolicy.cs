using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Sla;

/// <summary>
/// A named set of response and resolution commitments (SLA and Automation / Response and resolution
/// targets). Policies are evaluated in <see cref="EvaluationOrder"/> and the first whose conditions
/// match a ticket wins; the row flagged <see cref="IsDefault"/> is the fallback.
/// </summary>
public class SlaPolicy : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    public Guid BusinessCalendarId { get; set; }
    public BusinessCalendar BusinessCalendar { get; set; } = null!;

    /// <summary>Lower runs first. Ties are broken by <c>Id</c> so evaluation stays deterministic.</summary>
    public int EvaluationOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Percentage of the target at which the approaching-breach warning fires, for example 80.</summary>
    public int WarningThresholdPercent { get; set; } = 80;
    /// <summary>When true, a status whose kind pauses SLA stops the resolution clock.</summary>
    public bool PauseOnPendingCustomer { get; set; } = true;

    public ICollection<SlaTarget> Targets { get; set; } = new List<SlaTarget>();
    public ICollection<SlaPolicyCondition> Conditions { get; set; } = new List<SlaPolicyCondition>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
