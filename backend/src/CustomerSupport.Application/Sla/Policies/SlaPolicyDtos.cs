using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Sla.Policies;

public record SlaTargetDto(Guid PriorityId, string PriorityCode, string PriorityNameEn, string PriorityNameAr, int FirstResponseMinutes, int ResolutionMinutes);

public record SlaConditionDto(string Field, ConditionOperator Operator, string? Value);

/// <summary>One row per priority, submitted by the targets grid.</summary>
public record SlaTargetInput(Guid PriorityId, int FirstResponseMinutes, int ResolutionMinutes);

/// <summary>One row of the conditions builder.</summary>
public record SlaConditionInput(string Field, ConditionOperator Operator, string? Value);

public record SlaPolicyDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid BusinessCalendarId { get; init; }
    public string CalendarNameEn { get; init; } = string.Empty;
    public string CalendarNameAr { get; init; } = string.Empty;
    public int EvaluationOrder { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public int WarningThresholdPercent { get; init; }
    public bool PauseOnPendingCustomer { get; init; }
    public IReadOnlyList<SlaTargetDto> Targets { get; init; } = [];
    public IReadOnlyList<SlaConditionDto> Conditions { get; init; } = [];
}
