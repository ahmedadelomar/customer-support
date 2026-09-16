using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CustomerSupport.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Pushes newly created <see cref="Notification"/> rows over SignalR the instant they actually
/// commit (SLA and Automation / Alerts and notifications) — never before. Captured in
/// <see cref="SavingChangesAsync"/> (while the rows are still <c>Added</c> and have their generated
/// ids) and pushed only from <see cref="SavedChangesAsync"/>, so a rolled-back transaction pushes
/// nothing, matching the same rule that keeps it from leaving a stray database row.
/// </summary>
public class NotificationRealtimeInterceptor(IRealtimeNotifier notifier) : SaveChangesInterceptor
{
    private readonly List<Notification> _pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        _pending.Clear();

        if (eventData.Context is { } context)
        {
            _pending.AddRange(context.ChangeTracker.Entries<Notification>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity));
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var toPush = _pending.ToList();
        _pending.Clear();

        foreach (var notification in toPush)
        {
            await notifier.NotifyAsync(
                notification.UserId,
                new RealtimeNotification(
                    notification.Id, notification.EventType,
                    notification.Title.En, notification.Title.Ar,
                    notification.Body.En, notification.Body.Ar,
                    notification.Link, notification.Severity, notification.CreatedAt),
                cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
