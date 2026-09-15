using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets;
using CustomerSupport.Application.Workspace.Collaboration;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Adds an agent-only note to the thread. Deliberately does NOT write an
/// <see cref="Domain.Customers.Interaction"/>: nothing was exchanged with the customer, and
/// <c>IsInternalNote</c> is the only gate that keeps it out of outbound sends and the portal.
/// </summary>
[RequirePermission(Permissions.Tickets.InternalNote)]
public record AddInternalNoteCommand : IRequest<Guid>
{
    public Guid TicketId { get; init; }
    public string BodyText { get; init; } = string.Empty;
}

public class AddInternalNoteCommandValidator : AbstractValidator<AddInternalNoteCommand>
{
    public AddInternalNoteCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.BodyText).NotEmpty();
    }
}

public class AddInternalNoteCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    INotificationDispatcher notifications,
    IDateTimeProvider clock)
    : IRequestHandler<AddInternalNoteCommand, Guid>
{
    public async Task<Guid> Handle(AddInternalNoteCommand request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        var ticket = await db.Tickets.Include(t => t.Status).Include(t => t.Watchers)
            .FirstAsync(t => t.Id == request.TicketId, cancellationToken);

        TicketReadOnlyGuard.EnsureEditable(ticket, ticket.Status.IsTerminal);

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = ChannelKey.Internal,
            Direction = MessageDirection.Outbound,
            AuthorType = MessageAuthorType.Agent,
            AuthorId = currentUser.UserId,
            AuthorDisplayName = currentUser.UserName,
            BodyText = request.BodyText,
            IsInternalNote = true,
            SentAt = clock.UtcNow,
        };

        db.TicketMessages.Add(message);

        events.Record(ticket.Id, TicketEventType.InternalNoteAdded);

        var watchersBeforeThisNote = ticket.Watchers.ToList();

        await MentionProcessor.ProcessAsync(
            db, notifications, events, ticket, message,
            currentUser.UserId!.Value, currentUser.UserName ?? "", clock.UtcNow, cancellationToken);

        await TicketCollaborationNotifications.NotifyActivityAsync(
            notifications, ticket, watchersBeforeThisNote, currentUser.UserId!.Value, currentUser.UserName ?? "",
            isInternalNote: true, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return message.Id;
    }
}
