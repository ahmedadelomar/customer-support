using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Domain.Sla;

/// <summary>
/// The live SLA clock for one commitment on one ticket. Two rows per ticket, one per
/// <see cref="SlaTargetType"/>. This is the authority; the mirrored columns on <c>Ticket</c> exist
/// only so list queries avoid a join.
/// </summary>
public class TicketSlaClock : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public Guid SlaPolicyId { get; set; }
    public SlaTargetType TargetType { get; set; }
    public SlaClockStatus Status { get; set; } = SlaClockStatus.Running;

    public DateTimeOffset StartedAt { get; set; }
    /// <summary>Deadline in absolute time, recomputed whenever the clock resumes or the target changes.</summary>
    public DateTimeOffset DueAt { get; set; }
    public int TargetMinutes { get; set; }

    /// <summary>Working minutes consumed so far, updated when the clock pauses, resumes or completes.</summary>
    public int ElapsedMinutes { get; set; }
    public int PausedMinutes { get; set; }
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>Set once the commitment is satisfied, for example on first agent reply.</summary>
    public DateTimeOffset? MetAt { get; set; }
    public DateTimeOffset? BreachedAt { get; set; }
    /// <summary>Guards against sending the approaching-breach warning more than once.</summary>
    public bool WarningSent { get; set; }
}
