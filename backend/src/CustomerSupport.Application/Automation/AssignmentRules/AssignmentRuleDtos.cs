using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Automation.AssignmentRules;

public record AssignmentRuleConditionInput(string Field, ConditionOperator Operator, string? Value);

public record AssignmentRuleDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int EvaluationOrder { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<AssignmentRuleConditionInput> Conditions { get; init; } = [];
    public AssignmentStrategy Strategy { get; init; }
    public Guid? TargetDepartmentId { get; init; }
    public Guid? TargetTeamId { get; init; }
    public Guid? TargetUserId { get; init; }
    public bool RespectAgentAvailability { get; init; }
    public bool StopProcessing { get; init; }
    public int MatchCount { get; init; }
    public DateTimeOffset? LastMatchedAt { get; init; }
}
