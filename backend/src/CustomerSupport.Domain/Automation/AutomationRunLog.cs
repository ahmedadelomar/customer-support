using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Automation;

/// <summary>
/// Audit of every automation decision. Needed both to explain why a ticket was routed or escalated
/// and to enforce the cooldown and max-fire limits.
/// </summary>
public class AutomationRunLog : BaseEntity
{
    /// <summary>AssignmentRule or EscalationRule.</summary>
    public string RuleType { get; set; } = string.Empty;
    public Guid RuleId { get; set; }
    public string? RuleName { get; set; }
    public Guid TicketId { get; set; }

    /// <summary>Matched, Skipped or Failed.</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Short explanation shown in the admin rule tester.</summary>
    public string? Reason { get; set; }

    /// <summary>JSON describing what changed, for example the chosen agent.</summary>
    public string? ResultJson { get; set; }
    public string? Error { get; set; }

    public int DurationMs { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
