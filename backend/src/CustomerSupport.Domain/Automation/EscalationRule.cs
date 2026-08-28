using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Automation;

/// <summary>
/// Time or event driven rule that reacts to at-risk tickets (SLA and Automation / Escalation rules).
/// Evaluated by a recurring background job rather than on request, so it fires even when nobody is looking.
/// </summary>
public class EscalationRule : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    public int EvaluationOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public EscalationTrigger Trigger { get; set; }

    /// <summary>For ApproachingBreach, the percentage of the SLA target that must be consumed.</summary>
    public int? ThresholdPercent { get; set; }

    /// <summary>For NoAgentResponse, the idle minutes tolerated.</summary>
    public int? ThresholdMinutes { get; set; }

    /// <summary>For the reply-count and reopen-count triggers.</summary>
    public int? ThresholdCount { get; set; }

    /// <summary>Which SLA commitment the trigger watches; null means either.</summary>
    public SlaTargetType? TargetType { get; set; }

    /// <summary>Extra predicates narrowing which tickets the rule applies to.</summary>
    public string ConditionsJson { get; set; } = "[]";

    public EscalationActionType Action { get; set; }
    public Guid? ActionTargetUserId { get; set; }
    public Guid? ActionTargetTeamId { get; set; }
    public Guid? ActionTargetDepartmentId { get; set; }
    public Guid? ActionTargetPriorityId { get; set; }

    /// <summary>Notify every holder of this role, used with the NotifyManager action.</summary>
    public Guid? ActionNotifyRoleId { get; set; }

    /// <summary>Prevents the same rule firing repeatedly on one ticket within this window.</summary>
    public int CooldownMinutes { get; set; } = 60;

    /// <summary>Zero means unlimited. Caps how many times the rule may fire per ticket.</summary>
    public int MaxFiresPerTicket { get; set; } = 3;

    public int FireCount { get; set; }
    public DateTimeOffset? LastFiredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
