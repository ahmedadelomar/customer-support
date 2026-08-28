using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Organization;

/// <summary>Membership join row. <see cref="MaxConcurrentTickets"/> feeds the load-balanced assignment strategy.</summary>
public class TeamMember : BaseEntity
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public Guid UserId { get; set; }

    public bool IsLead { get; set; }
    public int MaxConcurrentTickets { get; set; } = 25;
    /// <summary>Ordinal used by round-robin so the rotation is stable across restarts.</summary>
    public int RotationOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; }
}
