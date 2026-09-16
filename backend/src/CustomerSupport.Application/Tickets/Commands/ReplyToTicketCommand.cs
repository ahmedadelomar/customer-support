using CustomerSupport.Application.Channels.Outbound;
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
/// Sends an outbound reply to the customer. Moves the ticket out of <c>New</c>, stamps
/// <see cref="Ticket.LastAgentReplyAt"/>, and records both a <see cref="TicketEventType.MessageAdded"/>
/// event and an outbound <see cref="Domain.Customers.Interaction"/> — this is a customer-visible
/// exchange, unlike an internal note.
/// </summary>
[RequirePermission(Permissions.Tickets.Reply)]
public record ReplyToTicketCommand : IRequest<Guid>
{
    public Guid TicketId { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public string? BodyHtml { get; init; }
    /// <summary>Set when this reply was inserted from a quick reply (Agent Dashboard / Quick replies), for attribution.</summary>
    public Guid? QuickReplyId { get; init; }
}

public class ReplyToTicketCommandValidator : AbstractValidator<ReplyToTicketCommand>
{
    public ReplyToTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.BodyText).NotEmpty();
    }
}

public class ReplyToTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    IInteractionRecorder interactions,
    ISlaEngine slaEngine,
    INotificationDispatcher notifications,
    IDateTimeProvider clock)
    : IRequestHandler<ReplyToTicketCommand, Guid>
{
    public async Task<Guid> Handle(ReplyToTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .Include(t => t.Watchers)
            .WhereBranchAccessible(currentUser)
            .WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        var status = await db.TicketStatuses.FirstAsync(s => s.Id == ticket.StatusId, cancellationToken);

        TicketReadOnlyGuard.EnsureEditable(ticket, status.IsTerminal);

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = ticket.Channel,
            Direction = MessageDirection.Outbound,
            AuthorType = MessageAuthorType.Agent,
            AuthorId = currentUser.UserId,
            AuthorDisplayName = currentUser.UserName,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            QuickReplyId = request.QuickReplyId,
            SentAt = clock.UtcNow,
        };

        db.TicketMessages.Add(message);

        if (ticket.Channel == ChannelKey.Email && ticket.ChannelAccountId is not null)
        {
            await QueueEmailReplyAsync(ticket, message, cancellationToken);
        }

        ticket.LastAgentReplyAt = clock.UtcNow;

        if (status.Kind == TicketStatusKind.New)
        {
            var openStatus = await db.TicketStatuses.FirstOrDefaultAsync(s => s.Kind == TicketStatusKind.Open, cancellationToken);
            if (openStatus is not null)
            {
                events.Record(ticket.Id, TicketEventType.StatusChanged,
                    field: nameof(Ticket.StatusId),
                    oldValue: status.Id.ToString(), newValue: openStatus.Id.ToString(),
                    oldDisplay: status.Name.En, newDisplay: openStatus.Name.En,
                    triggeredByRule: "AgentReply");

                ticket.StatusId = openStatus.Id;
            }
        }

        events.Record(ticket.Id, TicketEventType.MessageAdded,
            field: nameof(TicketMessage.Direction), newValue: nameof(MessageDirection.Outbound));

        interactions.Record(
            ticket.CustomerId, ticket.Channel, MessageDirection.Outbound,
            ticket.Subject, Truncate(request.BodyText), ticket.Id, nameof(Ticket), ticket.Id, currentUser.UserId);

        await TicketCollaborationNotifications.NotifyActivityAsync(
            notifications, ticket, ticket.Watchers, currentUser.UserId!.Value, currentUser.UserName ?? "",
            isInternalNote: false, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        // No-op until CS-501 lands; wired now so that story only has to implement it.
        await slaEngine.OnFirstAgentReplyAsync(ticket.Id, cancellationToken);

        return message.Id;
    }

    /// <summary>
    /// Queues the reply through the outbox (Communication Channels / Email channel, CS-301) —
    /// generates and stores this message's own <c>Message-ID</c> so the customer's next reply
    /// threads back onto it, and prefixes the subject with the ticket number the same way every
    /// outbound reply on this channel does.
    /// </summary>
    private async Task QueueEmailReplyAsync(Ticket ticket, TicketMessage message, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ticket.CustomerId, ct);
        if (string.IsNullOrWhiteSpace(customer?.PrimaryEmail))
        {
            return; // nothing to send to — the reply still lands in-app, just not by email
        }

        var account = await db.ChannelAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == ticket.ChannelAccountId, ct);
        if (account is null)
        {
            return;
        }

        var lastInbound = await db.TicketMessages.AsNoTracking()
            .Where(m => m.TicketId == ticket.Id && m.Direction == MessageDirection.Inbound && m.ExternalMessageId != null)
            .OrderByDescending(m => m.SentAt)
            .Select(m => m.ExternalMessageId)
            .FirstOrDefaultAsync(ct);

        var messageId = EmailChannelOutbox.NewMessageId(account.Identifier);
        message.ExternalMessageId = messageId;
        message.InReplyToExternalId = lastInbound;

        var subject = $"[{ticket.Number}] {ticket.Subject}";

        EmailChannelOutbox.Queue(db, clock, new EmailOutboundPayload(
            message.Id, ticket.Id, account.Id, ticket.BranchId,
            customer.PrimaryEmail, account.Identifier, subject, message.BodyText, message.BodyHtml,
            customer.PreferredLanguage, messageId, lastInbound));
    }

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
