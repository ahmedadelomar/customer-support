using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Workspace;

/// <summary>
/// A to-do owned by an agent (Agent Dashboard / Tasks and reminders). May hang off a ticket
/// or stand alone, and can carry reminders.
/// </summary>
public class AgentTask : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public Guid? TicketId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid AssignedToId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Pending;
    /// <summary>Reuses the ticket priority table so the workspace sorts tasks and tickets consistently.</summary>
    public Guid? PriorityId { get; set; }

    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }

    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
