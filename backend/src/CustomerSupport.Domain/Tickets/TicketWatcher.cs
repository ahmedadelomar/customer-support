using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Tickets;

/// <summary>
/// A follower of a ticket (Agent Dashboard / Team collaboration). Watchers receive activity
/// notifications without being the assignee.
/// </summary>
public class TicketWatcher : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid UserId { get; set; }

    /// <summary>True when an escalation rule added the watcher rather than a person.</summary>
    public bool AddedByAutomation { get; set; }

    public DateTimeOffset AddedAt { get; set; }
    public Guid? AddedById { get; set; }
}
