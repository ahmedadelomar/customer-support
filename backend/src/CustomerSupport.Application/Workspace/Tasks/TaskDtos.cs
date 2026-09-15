using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>One reminder attached to a task, as the task list and ticket panel show it.</summary>
public record TaskReminderDto
{
    public Guid Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset RemindAt { get; init; }
    public IReadOnlyList<NotificationChannel> Channels { get; init; } = Array.Empty<NotificationChannel>();
    public bool IsSent { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public bool IsDismissed { get; init; }
}

/// <summary>One agent task, whether standalone or linked to a ticket/customer.</summary>
public record AgentTaskDto
{
    public Guid Id { get; init; }
    public Guid? TicketId { get; init; }
    public string? TicketNumber { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerDisplayNameEn { get; init; }
    public string? CustomerDisplayNameAr { get; init; }
    public Guid AssignedToId { get; init; }
    public string? AssignedToNameEn { get; init; }
    public string? AssignedToNameAr { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public AgentTaskStatus Status { get; init; }
    public Guid? PriorityId { get; init; }
    public string? PriorityNameEn { get; init; }
    public string? PriorityNameAr { get; init; }
    public string? PriorityColorHex { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    /// <summary>True when overdue and not yet completed/cancelled — computed server-side so every list agrees.</summary>
    public bool IsOverdue { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public IReadOnlyList<TaskReminderDto> Reminders { get; init; } = Array.Empty<TaskReminderDto>();
    public bool CanComplete { get; init; }
    public bool CanEdit { get; init; }
}
