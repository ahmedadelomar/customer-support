using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Workspace;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>
/// Maps <see cref="AgentTask"/> rows to <see cref="AgentTaskDto"/>, shared by every task query so
/// they can never disagree about overdue/permission logic. <c>Ticket</c>/<c>Customer</c>/<c>TicketPriority</c>
/// are loose references on <see cref="AgentTask"/> (no FK navigation, same reasoning as
/// <c>Interaction</c>'s <c>TicketId</c>), so display values are resolved as batched lookups rather
/// than an EF <c>Include</c>. Callers must <c>.Include(t => t.Reminders)</c> before mapping — that one
/// IS a real navigation.
/// </summary>
internal static class AgentTaskDtoMapper
{
    public static async Task<List<AgentTaskDto>> MapAsync(
        List<AgentTask> tasks,
        IAppDbContext db,
        IUserDisplayNameResolver userNames,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        CancellationToken ct)
    {
        if (tasks.Count == 0)
        {
            return [];
        }

        var ticketIds = tasks.Where(t => t.TicketId is not null).Select(t => t.TicketId!.Value).Distinct().ToList();
        var ticketNumbers = ticketIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Tickets.AsNoTracking().Where(t => ticketIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Number })
                .ToDictionaryAsync(t => t.Id, t => t.Number, ct);

        var customerIds = tasks.Where(t => t.CustomerId is not null).Select(t => t.CustomerId!.Value).Distinct().ToList();
        var customers = customerIds.Count == 0
            ? new Dictionary<Guid, (string En, string Ar)>()
            : await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.DisplayName.En, c.DisplayName.Ar })
                .ToDictionaryAsync(c => c.Id, c => (c.En, c.Ar), ct);

        var priorityIds = tasks.Where(t => t.PriorityId is not null).Select(t => t.PriorityId!.Value).Distinct().ToList();
        var priorities = priorityIds.Count == 0
            ? new Dictionary<Guid, (string En, string Ar, string ColorHex)>()
            : await db.TicketPriorities.AsNoTracking().Where(p => priorityIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name.En, p.Name.Ar, p.ColorHex })
                .ToDictionaryAsync(p => p.Id, p => (p.En, p.Ar, p.ColorHex), ct);

        var agentIds = tasks.Select(t => t.AssignedToId).Distinct();
        var agentNames = await userNames.ResolveAsync(agentIds, ct);

        var now = clock.UtcNow;
        var canManage = currentUser.HasPermission(Permissions.Workspace.ManageTasks);

        return tasks.Select(t => new AgentTaskDto
        {
            Id = t.Id,
            TicketId = t.TicketId,
            TicketNumber = t.TicketId is { } tId && ticketNumbers.TryGetValue(tId, out var num) ? num : null,
            CustomerId = t.CustomerId,
            CustomerDisplayNameEn = t.CustomerId is { } cId && customers.TryGetValue(cId, out var cust) ? cust.En : null,
            CustomerDisplayNameAr = t.CustomerId is { } cId2 && customers.TryGetValue(cId2, out var cust2) ? cust2.Ar : null,
            AssignedToId = t.AssignedToId,
            AssignedToNameEn = agentNames.TryGetValue(t.AssignedToId, out var name) ? name.En : null,
            AssignedToNameAr = agentNames.TryGetValue(t.AssignedToId, out var name2) ? name2.Ar : null,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status,
            PriorityId = t.PriorityId,
            PriorityNameEn = t.PriorityId is { } pId && priorities.TryGetValue(pId, out var pr) ? pr.En : null,
            PriorityNameAr = t.PriorityId is { } pId2 && priorities.TryGetValue(pId2, out var pr2) ? pr2.Ar : null,
            PriorityColorHex = t.PriorityId is { } pId3 && priorities.TryGetValue(pId3, out var pr3) ? pr3.ColorHex : null,
            DueAt = t.DueAt,
            IsOverdue = t.DueAt is { } due && due < now && t.Status is AgentTaskStatus.Pending or AgentTaskStatus.InProgress,
            CompletedAt = t.CompletedAt,
            Reminders = t.Reminders
                .OrderBy(r => r.RemindAt)
                .Select(r => new TaskReminderDto
                {
                    Id = r.Id,
                    Message = r.Message,
                    RemindAt = r.RemindAt,
                    Channels = ParseChannels(r.Channels),
                    IsSent = r.IsSent,
                    SentAt = r.SentAt,
                    IsDismissed = r.IsDismissed,
                })
                .ToList(),
            CanComplete = t.AssignedToId == currentUser.UserId || canManage,
            CanEdit = canManage,
        }).ToList();
    }

    public static IReadOnlyList<NotificationChannel> ParseChannels(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(v => Enum.TryParse<NotificationChannel>(v, true, out var c) ? c : (NotificationChannel?)null)
           .Where(c => c is not null)
           .Select(c => c!.Value)
           .ToList();

    public static string FormatChannels(IReadOnlyCollection<NotificationChannel>? channels) =>
        channels is null || channels.Count == 0
            ? nameof(NotificationChannel.InApp)
            : string.Join(",", channels.Distinct());
}
