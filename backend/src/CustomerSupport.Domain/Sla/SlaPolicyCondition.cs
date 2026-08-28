using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Sla;

/// <summary>
/// One predicate that must hold for a policy to apply. All conditions on a policy are combined with
/// AND, which keeps evaluation cheap and the admin UI simple.
/// </summary>
public class SlaPolicyCondition : BaseEntity
{
    public Guid SlaPolicyId { get; set; }
    public SlaPolicy SlaPolicy { get; set; } = null!;

    /// <summary>Ticket field path, for example <c>CategoryId</c>, <c>Channel</c> or <c>Customer.Tier</c>.</summary>
    public string Field { get; set; } = string.Empty;
    public ConditionOperator Operator { get; set; }
    /// <summary>Comparison value, or a comma-separated list for the In and NotIn operators.</summary>
    public string? Value { get; set; }
}
