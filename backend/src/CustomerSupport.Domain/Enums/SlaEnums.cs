namespace CustomerSupport.Domain.Enums;

/// <summary>The two SLA commitments tracked per ticket.</summary>
public enum SlaTargetType
{
    FirstResponse = 0,
    Resolution = 1,
}

public enum SlaClockStatus
{
    Running = 0,
    Paused = 1,
    Met = 2,
    Breached = 3,
    Cancelled = 4,
}

/// <summary>How an <c>AssignmentRule</c> picks the agent once its conditions match.</summary>
public enum AssignmentStrategy
{
    /// <summary>Cycle through the target team's active members in order.</summary>
    RoundRobin = 0,
    /// <summary>Pick the active member with the fewest open tickets.</summary>
    LoadBalanced = 1,
    /// <summary>Pick among members whose skills cover the ticket category.</summary>
    SkillBased = 2,
    /// <summary>Always assign to a fixed user.</summary>
    Direct = 3,
    /// <summary>Leave unassigned in the team queue for agents to pull.</summary>
    QueueOnly = 4,
}

public enum EscalationTrigger
{
    /// <summary>Fires when the elapsed SLA time crosses <c>ThresholdPercent</c> of the target.</summary>
    ApproachingBreach = 0,
    Breached = 1,
    /// <summary>No agent reply for N minutes.</summary>
    NoAgentResponse = 2,
    /// <summary>Customer replied N times without resolution.</summary>
    CustomerReplyCount = 3,
    /// <summary>Ticket reopened more than N times.</summary>
    ReopenCount = 4,
}

public enum EscalationActionType
{
    NotifyManager = 0,
    Reassign = 1,
    RaisePriority = 2,
    ChangeDepartment = 3,
    IncreaseEscalationLevel = 4,
    AddWatcher = 5,
}

public enum ConditionOperator
{
    Equals = 0,
    NotEquals = 1,
    In = 2,
    NotIn = 3,
    Contains = 4,
    GreaterThan = 5,
    LessThan = 6,
    IsNull = 7,
    IsNotNull = 8,
}
