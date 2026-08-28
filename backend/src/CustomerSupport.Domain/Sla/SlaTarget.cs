using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Sla;

/// <summary>The minute budgets a policy grants for one priority. One row per priority per policy.</summary>
public class SlaTarget : BaseEntity
{
    public Guid SlaPolicyId { get; set; }
    public SlaPolicy SlaPolicy { get; set; } = null!;
    public Guid PriorityId { get; set; }

    /// <summary>Working minutes allowed before the first agent reply.</summary>
    public int FirstResponseMinutes { get; set; }
    /// <summary>Working minutes allowed before the ticket reaches a resolved status.</summary>
    public int ResolutionMinutes { get; set; }
}
