using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Automation;

/// <summary>
/// Routing rule evaluated on ticket creation and on manual re-route (SLA and Automation /
/// Automatic assignment). Rules run in <see cref="EvaluationOrder"/> and the first match wins.
/// </summary>
public class AssignmentRule : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    public int EvaluationOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>JSON array of predicates, same shape as <c>SlaPolicyCondition</c>, combined with AND.</summary>
    public string ConditionsJson { get; set; } = "[]";

    public AssignmentStrategy Strategy { get; set; } = AssignmentStrategy.RoundRobin;
    public Guid? TargetDepartmentId { get; set; }
    public Guid? TargetTeamId { get; set; }

    /// <summary>Required only for the Direct strategy.</summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>Skip agents who are away or over capacity and fall through to the next rule.</summary>
    public bool RespectAgentAvailability { get; set; } = true;

    /// <summary>Stop evaluating later rules once this one matches. Off allows layered enrichment.</summary>
    public bool StopProcessing { get; set; } = true;

    public int MatchCount { get; set; }
    public DateTimeOffset? LastMatchedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
