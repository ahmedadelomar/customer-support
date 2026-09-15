using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Reminders;

/// <summary>One fired-but-unacknowledged reminder, for the persistent toast queue.</summary>
public record PendingReminderDto
{
    public Guid Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset SentAt { get; init; }
    public Guid? TicketId { get; init; }
    public string? TicketNumber { get; init; }
}

/// <summary>
/// The reminders that have fired for the caller and are still waiting to be snoozed or dismissed —
/// what the shell polls to show a persistent toast. Not in the story's own API contract table, but
/// nothing else exposes this: without it, the frontend has no way to learn a reminder fired at all.
/// A reminder is excluded once its linked task is completed or cancelled — a finished task's reminder
/// is no longer worth nagging about, even though the row itself is left alone (see
/// <see cref="Tasks.AgentTaskCompletion"/>).
/// </summary>
[RequirePermission(Permissions.Workspace.ViewOwnTasks)]
public record GetPendingRemindersQuery : IRequest<IReadOnlyList<PendingReminderDto>>;

public class GetPendingRemindersQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPendingRemindersQuery, IReadOnlyList<PendingReminderDto>>
{
    public async Task<IReadOnlyList<PendingReminderDto>> Handle(GetPendingRemindersQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Reminders.AsNoTracking()
            .Where(r => r.UserId == currentUser.UserId && r.IsSent && !r.IsDismissed)
            .Select(r => new
            {
                r.Id,
                r.Message,
                r.SentAt,
                r.TicketId,
                LinkedTaskStatus = r.AgentTaskId == null
                    ? (AgentTaskStatus?)null
                    : db.AgentTasks.Where(t => t.Id == r.AgentTaskId).Select(t => (AgentTaskStatus?)t.Status).FirstOrDefault(),
            })
            .Where(r => r.LinkedTaskStatus == null || (r.LinkedTaskStatus != AgentTaskStatus.Completed && r.LinkedTaskStatus != AgentTaskStatus.Cancelled))
            .OrderByDescending(r => r.SentAt)
            .ToListAsync(cancellationToken);

        var ticketIds = rows.Where(r => r.TicketId is not null).Select(r => r.TicketId!.Value).Distinct().ToList();
        var ticketNumbers = ticketIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Tickets.AsNoTracking().Where(t => ticketIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Number })
                .ToDictionaryAsync(t => t.Id, t => t.Number, cancellationToken);

        return rows.Select(r => new PendingReminderDto
        {
            Id = r.Id,
            Message = r.Message,
            SentAt = r.SentAt!.Value,
            TicketId = r.TicketId,
            TicketNumber = r.TicketId is { } tId && ticketNumbers.TryGetValue(tId, out var num) ? num : null,
        }).ToList();
    }
}
