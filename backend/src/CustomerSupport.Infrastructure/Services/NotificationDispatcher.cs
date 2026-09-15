using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Writes the in-app <see cref="Notification"/> row. Channel fan-out (email, SMS, push) is CS-504's
/// job — until it lands, this is the whole implementation, per that story's own note that callers
/// should write the row and skip the fan-out rather than wait. Adds to the unit of work; the caller
/// saves, same convention as <see cref="TicketEventRecorder"/>.
/// </summary>
public class NotificationDispatcher(AppDbContext db, IDateTimeProvider clock) : INotificationDispatcher
{
    public Task DispatchAsync(
        Guid userId,
        string eventType,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        string severity = "Info",
        CancellationToken ct = default)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            EventType = eventType,
            Title = new LocalizedText(titleEn, titleAr),
            Body = new LocalizedText(bodyEn, bodyAr),
            Link = link,
            Severity = severity,
            CreatedAt = clock.UtcNow,
        });

        return Task.CompletedTask;
    }
}
