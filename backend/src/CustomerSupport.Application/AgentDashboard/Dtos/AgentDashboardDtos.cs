namespace CustomerSupport.Application.AgentDashboard.Dtos;

/// <summary>Computed in a single grouped query — see <c>GetAgentDashboardQueryHandler</c>.</summary>
public record AgentDashboardTilesDto
{
    public int Assigned { get; init; }
    public int DueToday { get; init; }
    public int ApproachingSla { get; init; }
    public int Breached { get; init; }
}

/// <summary>
/// Personal count beside the team's, per the story's own rule: context, not a leaderboard —
/// never a per-agent ranking.
/// </summary>
public record AgentDashboardStatsDto
{
    public int ResolvedThisWeek { get; init; }
    public double TeamAverageResolvedThisWeek { get; init; }
}

/// <summary>One row of the next-up queue: always assigned to the caller (see the query's own filter).</summary>
public record AgentQueueItemDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerDisplayNameEn { get; init; } = string.Empty;
    public string CustomerDisplayNameAr { get; init; } = string.Empty;
    public Guid PriorityId { get; init; }
    public string PriorityNameEn { get; init; } = string.Empty;
    public string PriorityNameAr { get; init; } = string.Empty;
    public string PriorityColorHex { get; init; } = string.Empty;
    public DateTimeOffset? ResolutionDueAt { get; init; }
    public bool IsFirstResponseBreached { get; init; }
    public bool IsResolutionBreached { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Everything the dashboard needs in one response — tiles, queue and stats — per the story's own
/// "one request, one response" rule. <c>CanViewQueue</c>/<c>CanViewStats</c> let the frontend render a
/// coherent smaller dashboard for a restricted role instead of guessing from empty data.
/// </summary>
public record AgentDashboardDto
{
    public bool CanViewQueue { get; init; }
    public bool CanViewStats { get; init; }
    public AgentDashboardTilesDto Tiles { get; init; } = new();
    public IReadOnlyList<AgentQueueItemDto> Queue { get; init; } = Array.Empty<AgentQueueItemDto>();
    public AgentDashboardStatsDto Stats { get; init; } = new();
}
