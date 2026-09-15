using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Records a customer-side message on a ticket. This is the shared core the story calls for —
/// "used by both the portal and Section 3 channels" — neither of which exists yet; a real channel
/// adapter or the portal will call this once built, so the reopen/follow-up rules below have exactly
/// one implementation no matter which one ends up calling it first.
/// </summary>
[RequirePermission(Permissions.Tickets.Reply)]
public record RecordInboundCustomerMessageCommand : IRequest<InboundMessageResultDto>
{
    public Guid TicketId { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public string? BodyHtml { get; init; }
    /// <summary>Provider message id, for idempotent ingestion once a real channel calls this.</summary>
    public string? ExternalMessageId { get; init; }
}

public record InboundMessageResultDto(Guid MessageId, Guid TicketId, bool Reopened, Guid? FollowUpTicketId);

public class RecordInboundCustomerMessageCommandValidator : AbstractValidator<RecordInboundCustomerMessageCommand>
{
    public RecordInboundCustomerMessageCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.BodyText).NotEmpty();
    }
}

public class RecordInboundCustomerMessageCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    IInteractionRecorder interactions,
    ISlaEngine sla,
    IReferenceNumberGenerator numbers,
    IDateTimeProvider clock)
    : IRequestHandler<RecordInboundCustomerMessageCommand, InboundMessageResultDto>
{
    public async Task<InboundMessageResultDto> Handle(
        RecordInboundCustomerMessageCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .Include(t => t.Status)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        // Terminal (Closed/Cancelled): reopening weeks later would distort resolution-time
        // reporting, so a linked follow-up ticket is created instead.
        if (ticket.Status.IsTerminal)
        {
            return await CreateFollowUpAsync(ticket, request, cancellationToken);
        }

        var customer = await db.Customers.FirstAsync(c => c.Id == ticket.CustomerId, cancellationToken);

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = ticket.Channel,
            Direction = MessageDirection.Inbound,
            AuthorType = MessageAuthorType.Customer,
            AuthorId = ticket.CustomerId,
            AuthorDisplayName = customer.DisplayName.For(customer.PreferredLanguage),
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            ExternalMessageId = request.ExternalMessageId,
            SentAt = clock.UtcNow,
        };

        db.TicketMessages.Add(message);

        ticket.LastCustomerReplyAt = clock.UtcNow;
        ticket.CustomerReplyCount += 1;

        var reopened = ticket.Status.Kind == TicketStatusKind.Resolved;

        if (reopened)
        {
            var openStatus = await db.TicketStatuses
                .Where(s => s.Kind == TicketStatusKind.Open)
                .OrderBy(s => s.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("No Open-kind ticket status is configured.");

            var oldStatus = ticket.Status;

            ticket.StatusId = openStatus.Id;
            ticket.ReopenCount += 1;
            ticket.ResolvedAt = null;
            ticket.ResolvedById = null;

            events.Record(ticket.Id, TicketEventType.Reopened,
                field: nameof(Ticket.StatusId),
                oldValue: oldStatus.Id.ToString(), newValue: openStatus.Id.ToString(),
                oldDisplay: oldStatus.Name.For(ticket.Language), newDisplay: openStatus.Name.For(ticket.Language));
        }
        else
        {
            events.Record(ticket.Id, TicketEventType.MessageAdded,
                field: nameof(TicketMessage.Direction), newValue: nameof(MessageDirection.Inbound));
        }

        interactions.Record(
            ticket.CustomerId, ticket.Channel, MessageDirection.Inbound,
            ticket.Subject, Truncate(request.BodyText), ticket.Id, nameof(Ticket), ticket.Id);

        await db.SaveChangesAsync(cancellationToken);

        // The clock resumes rather than restarts — CS-501 owns that arithmetic; this call site is
        // what it hooks into.
        if (reopened)
        {
            await sla.OnStatusChangedAsync(ticket.Id, cancellationToken);
        }

        return new InboundMessageResultDto(message.Id, ticket.Id, reopened, null);
    }

    private async Task<InboundMessageResultDto> CreateFollowUpAsync(
        Ticket original, RecordInboundCustomerMessageCommand request, CancellationToken ct)
    {
        var customer = await db.Customers.FirstAsync(c => c.Id == original.CustomerId, ct);
        var defaultStatus = await db.TicketStatuses.FirstAsync(s => s.IsDefault, ct);

        var truncatedSubject = Truncate(original.Subject, 490);
        var followUpSubject = truncatedSubject.StartsWith("Re: ") ? truncatedSubject : $"Re: {truncatedSubject}";

        var followUp = new Ticket
        {
            Number = await numbers.NextTicketNumberAsync(ct),
            CustomerId = original.CustomerId,
            BranchId = original.BranchId,
            Subject = followUpSubject,
            Description = request.BodyText,
            Language = original.Language,
            CategoryId = original.CategoryId,
            PriorityId = original.PriorityId,
            StatusId = defaultStatus.Id,
            Channel = original.Channel,
            DepartmentId = original.DepartmentId,
        };

        db.Tickets.Add(followUp);

        followUp.Messages.Add(new TicketMessage
        {
            TicketId = followUp.Id,
            Channel = original.Channel,
            Direction = MessageDirection.Inbound,
            AuthorType = MessageAuthorType.Customer,
            AuthorId = original.CustomerId,
            AuthorDisplayName = customer.DisplayName.For(customer.PreferredLanguage),
            BodyText = request.BodyText,
            ExternalMessageId = request.ExternalMessageId,
            SentAt = clock.UtcNow,
        });

        events.Record(followUp.Id, TicketEventType.Created);

        events.Record(original.Id, TicketEventType.FollowUpCreated,
            field: nameof(Ticket.Id),
            newValue: followUp.Id.ToString(), newDisplay: followUp.Number,
            metadataJson: JsonSerializer.Serialize(new { FollowUpTicketId = followUp.Id }));

        interactions.Record(
            original.CustomerId, original.Channel, MessageDirection.Inbound,
            followUp.Subject, Truncate(request.BodyText), followUp.Id, nameof(Ticket), followUp.Id);

        await db.SaveChangesAsync(ct);

        return new InboundMessageResultDto(followUp.Messages.First().Id, followUp.Id, false, followUp.Id);
    }

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
