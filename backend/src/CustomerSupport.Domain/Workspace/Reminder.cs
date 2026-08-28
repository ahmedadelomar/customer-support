using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Workspace;

/// <summary>
/// A timed nudge for a user, polled by the reminder dispatch job. Attaches to a task, a ticket,
/// or neither for a standalone personal reminder.
/// </summary>
public class Reminder : BaseEntity
{
    public Guid? AgentTaskId { get; set; }
    public AgentTask? AgentTask { get; set; }
    public Guid? TicketId { get; set; }

    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset RemindAt { get; set; }

    /// <summary>Which channels to notify on, as a comma-separated <c>NotificationChannel</c> list.</summary>
    public string Channels { get; set; } = "InApp";

    public bool IsSent { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public bool IsDismissed { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
}
