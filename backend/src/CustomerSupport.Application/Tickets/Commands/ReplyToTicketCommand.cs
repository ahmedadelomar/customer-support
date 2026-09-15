using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets;
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
    IDateTimeProvider clock)
    : IRequestHandler<ReplyToTicketCommand, Guid>
{
    public async Task<Guid> Handle(ReplyToTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
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
            SentAt = clock.UtcNow,
        };

        db.TicketMessages.Add(message);

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

        await db.SaveChangesAsync(cancellationToken);

        // No-op until CS-501 lands; wired now so that story only has to implement it.
        await slaEngine.OnFirstAgentReplyAsync(ticket.Id, cancellationToken);

        return message.Id;
    }

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
