using CustomerSupport.Application.Automation.AssignmentRules;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Automation.EscalationRules;

public record EscalationRuleDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int EvaluationOrder { get; init; }
    public bool IsActive { get; init; }
    public EscalationTrigger Trigger { get; init; }
    public int? ThresholdPercent { get; init; }
    public int? ThresholdMinutes { get; init; }
    public int? ThresholdCount { get; init; }
    public SlaTargetType? TargetType { get; init; }
    public IReadOnlyList<AssignmentRuleConditionInput> Conditions { get; init; } = [];
    public EscalationActionType Action { get; init; }
    public Guid? ActionTargetUserId { get; init; }
    public Guid? ActionTargetTeamId { get; init; }
    public Guid? ActionTargetDepartmentId { get; init; }
    public Guid? ActionTargetPriorityId { get; init; }
    public Guid? ActionNotifyRoleId { get; init; }
    public int CooldownMinutes { get; init; }
    public int MaxFiresPerTicket { get; init; }
    public int FireCount { get; init; }
    public DateTimeOffset? LastFiredAt { get; init; }
}
