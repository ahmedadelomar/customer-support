using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Workspace;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

/// <summary>
/// Completes a task and cancels its unsent reminders — shared by <see cref="CompleteTaskCommand"/> and
/// the ticket status-change flow's "complete linked tasks" option, so the two can never disagree about
/// what "completing a task" does. A reminder that already fired is left alone: it is a historical
/// record, not something to erase.
/// </summary>
internal static class AgentTaskCompletion
{
    public static async Task CompleteAsync(
        AgentTask task, Guid? completedById, IAppDbContext db, IDateTimeProvider clock, CancellationToken ct)
    {
        task.Status = AgentTaskStatus.Completed;
        task.CompletedAt = clock.UtcNow;
        task.CompletedById = completedById;

        await db.Reminders
            .Where(r => r.AgentTaskId == task.Id && !r.IsSent && !r.IsDismissed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsDismissed, true)
                .SetProperty(r => r.DismissedAt, clock.UtcNow), ct);
    }
}
