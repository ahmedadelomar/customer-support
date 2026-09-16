using CustomerSupport.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation.Notifications;

public record NotificationDto(
    Guid Id, string EventType, string TitleEn, string TitleAr, string BodyEn, string BodyAr,
    string? Link, string Severity, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

/// <summary>
/// The caller's own notifications, unread first then newest first. Paged with plain skip/take
/// rather than a true keyset cursor — the "unread first" ordering the story calls for is not a
/// single monotonic sort key, which is what keyset pagination needs to avoid a WHERE-based cursor;
/// skip/take stays correct for the sizes one user's own notification list ever reaches.
/// </summary>
public record GetNotificationsQuery(int Skip = 0, int Take = 30) : IRequest<IReadOnlyList<NotificationDto>>;

public class GetNotificationsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == currentUser.UserId)
            .OrderBy(n => n.ReadAt != null)
            .ThenByDescending(n => n.CreatedAt)
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 100))
            .ToListAsync(cancellationToken);

        return rows.Select(n => new NotificationDto(
            n.Id, n.EventType, n.Title.En, n.Title.Ar, n.Body.En, n.Body.Ar, n.Link, n.Severity, n.CreatedAt, n.ReadAt))
            .ToList();
    }
}

public record GetUnreadNotificationCountQuery : IRequest<int>;

public class GetUnreadNotificationCountQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    public Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken) =>
        db.Notifications.CountAsync(n => n.UserId == currentUser.UserId && n.ReadAt == null, cancellationToken);
}
