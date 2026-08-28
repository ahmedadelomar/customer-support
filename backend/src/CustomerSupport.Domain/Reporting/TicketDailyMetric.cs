using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Reporting;

/// <summary>
/// Pre-aggregated daily rollup, one row per date and dimension combination. Dashboards and reports
/// read this instead of scanning the ticket table, which is what keeps management screens fast as
/// ticket volume grows. Rebuilt nightly and incrementally on ticket close.
/// </summary>
public class TicketDailyMetric : BaseEntity
{
    public DateOnly Date { get; set; }

    // --- Dimensions. Null means the row aggregates across that dimension. ---
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? PriorityId { get; set; }
    public int? Channel { get; set; }

    // --- Volume ---
    public int CreatedCount { get; set; }
    public int ResolvedCount { get; set; }
    public int ClosedCount { get; set; }
    public int ReopenedCount { get; set; }
    public int EscalatedCount { get; set; }
    public int BacklogCount { get; set; }

    // --- SLA ---
    public int FirstResponseMetCount { get; set; }
    public int FirstResponseBreachedCount { get; set; }
    public int ResolutionMetCount { get; set; }
    public int ResolutionBreachedCount { get; set; }
    public long TotalFirstResponseMinutes { get; set; }
    public long TotalResolutionMinutes { get; set; }

    // --- Satisfaction. Store sum and count so averages recombine correctly across rows. ---
    public int CsatResponseCount { get; set; }
    public int CsatScoreSum { get; set; }

    // --- Automation and AI ---
    public int AutoAssignedCount { get; set; }
    public int AiSuggestionsOfferedCount { get; set; }
    public int AiSuggestionsAcceptedCount { get; set; }
    public int BotResolvedCount { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
