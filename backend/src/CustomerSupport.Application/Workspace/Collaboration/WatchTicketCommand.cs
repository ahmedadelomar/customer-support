using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>Follows a ticket the caller is not necessarily assigned to. Idempotent: watching twice is a no-op.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record WatchTicketCommand(Guid TicketId) : IRequest;

public class WatchTicketCommandHandler(
    IAppDbContext db, ICurrentUser currentUser, ITicketEventRecorder events,
    IUserDisplayNameResolver userNames, IDateTimeProvider clock)
    : IRequestHandler<WatchTicketCommand>
{
    public async Task Handle(WatchTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.Include(t => t.Watchers)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.Watchers.Any(w => w.UserId == currentUser.UserId))
        {
            return;
        }

        ticket.Watchers.Add(new TicketWatcher
        {
            TicketId = ticket.Id,
            UserId = currentUser.UserId!.Value,
            AddedByAutomation = false,
            AddedById = currentUser.UserId,
            AddedAt = clock.UtcNow,
        });

        var names = await userNames.ResolveAsync([currentUser.UserId!.Value], cancellationToken);
        events.Record(ticket.Id, TicketEventType.WatcherAdded,
            newValue: currentUser.UserId.ToString(), newDisplay: names.GetValueOrDefault(currentUser.UserId.Value)?.En ?? currentUser.UserName);

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Stops following a ticket. Idempotent: unwatching when not a watcher is a no-op.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record UnwatchTicketCommand(Guid TicketId) : IRequest;

public class UnwatchTicketCommandHandler(
    IAppDbContext db, ICurrentUser currentUser, ITicketEventRecorder events, IUserDisplayNameResolver userNames)
    : IRequestHandler<UnwatchTicketCommand>
{
    public async Task Handle(UnwatchTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.Include(t => t.Watchers)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        var watcher = ticket.Watchers.FirstOrDefault(w => w.UserId == currentUser.UserId);
        if (watcher is null)
        {
            return;
        }

        db.TicketWatchers.Remove(watcher);

        var names = await userNames.ResolveAsync([currentUser.UserId!.Value], cancellationToken);
        events.Record(ticket.Id, TicketEventType.WatcherRemoved,
            newValue: currentUser.UserId.ToString(), newDisplay: names.GetValueOrDefault(currentUser.UserId.Value)?.En ?? currentUser.UserName);

        await db.SaveChangesAsync(cancellationToken);
    }
}
