using System.Security.Cryptography;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Portal;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Moves a ticket to a new status. Every side effect — requiring a resolution note, stamping
/// <c>ResolvedAt</c>/<c>ClosedAt</c>, pausing the SLA clock — is driven by the target status's
/// <see cref="TicketStatus.Kind"/>, never by its name or code: an administrator renaming "Pending
/// Customer" must never change behaviour.
/// </summary>
[RequirePermission(Permissions.Tickets.ChangeStatus)]
public record ChangeTicketStatusCommand : IRequest
{
    public Guid TicketId { get; init; }
    public Guid StatusId { get; init; }
    /// <summary>Required only when the target status is Resolved-kind.</summary>
    public string? ResolutionNote { get; init; }
}

public class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.StatusId).NotEmpty();
    }
}

public class ChangeTicketStatusCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    ISlaEngine sla,
    IDateTimeProvider clock)
    : IRequestHandler<ChangeTicketStatusCommand>
{
    public async Task Handle(ChangeTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .Include(t => t.Status)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.MergedIntoTicketId is not null)
        {
            throw new ConflictException("This ticket was merged into another and is read-only.");
        }

        // Terminal tickets are otherwise read-only (see the guards in Update/Reply/Note/Assign) —
        // this command is deliberately the one escape valve, so an agent can manually reopen a
        // wrongly auto-closed or cancelled ticket. Automatic reopening on a customer reply is a
        // separate path (`RecordInboundCustomerMessageCommand`) that never reopens a terminal ticket.
        var newStatus = await db.TicketStatuses.FirstOrDefaultAsync(s => s.Id == request.StatusId, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketStatus), request.StatusId);

        if (newStatus.Kind == TicketStatusKind.Resolved && string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            throw new Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["resolutionNote"] = ["A resolution note is required when resolving a ticket."],
            });
        }

        // Captured before mutating — recording the event afterwards would show the new name twice.
        var oldStatus = ticket.Status;
        var oldDisplay = oldStatus.Name.For(ticket.Language);
        var newDisplay = newStatus.Name.For(ticket.Language);

        ticket.StatusId = newStatus.Id;

        switch (newStatus.Kind)
        {
            case TicketStatusKind.Resolved:
                ticket.ResolvedAt = clock.UtcNow;
                ticket.ResolvedById = currentUser.UserId;
                ticket.ResolutionNote = request.ResolutionNote;
                break;
            case TicketStatusKind.Closed or TicketStatusKind.Cancelled:
                ticket.ClosedAt = clock.UtcNow;
                break;
        }

        events.Record(ticket.Id, TicketEventType.StatusChanged,
            field: nameof(Ticket.StatusId),
            oldValue: oldStatus.Id.ToString(), newValue: newStatus.Id.ToString(),
            oldDisplay: oldDisplay, newDisplay: newDisplay);

        if (newStatus.Kind == TicketStatusKind.Resolved)
        {
            QueueSatisfactionSurvey(ticket);
        }

        await db.SaveChangesAsync(cancellationToken);

        // After the save, so the engine reads committed state.
        await sla.OnStatusChangedAsync(ticket.Id, cancellationToken);
        if (newStatus.Kind == TicketStatusKind.Resolved)
        {
            await sla.OnResolvedAsync(ticket.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Queues a CSAT survey (CS-805 sends it). Writing the row now — rather than waiting for that
    /// story — means resolving a ticket never silently skips the survey step.
    /// </summary>
    private void QueueSatisfactionSurvey(Ticket ticket)
    {
        db.CsatSurveys.Add(new CsatSurvey
        {
            BranchId = ticket.BranchId,
            TicketId = ticket.Id,
            CustomerId = ticket.CustomerId,
            AgentId = ticket.AssignedAgentId,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            Language = ticket.Language,
            SentAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddDays(14),
        });
    }
}
