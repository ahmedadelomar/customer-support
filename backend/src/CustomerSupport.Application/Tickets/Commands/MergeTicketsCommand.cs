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
/// Merges the source ticket into the target: messages move over, the source becomes read-only and
/// terminal, and both histories record the merge. A cross-customer merge is almost always a mistake,
/// so it is refused unless explicitly overridden.
/// </summary>
[RequirePermission(Permissions.Tickets.Merge)]
public record MergeTicketsCommand : IRequest
{
    public Guid TicketId { get; init; }
    public Guid TargetTicketId { get; init; }
    public string? Reason { get; init; }
    public bool AllowCrossCustomer { get; init; }
}

public class MergeTicketsCommandValidator : AbstractValidator<MergeTicketsCommand>
{
    public MergeTicketsCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.TargetTicketId).NotEmpty();
        RuleFor(x => x).Must(x => x.TicketId != x.TargetTicketId)
            .WithMessage("A ticket cannot be merged into itself.");
    }
}

public class MergeTicketsCommandHandler(IAppDbContext db, ICurrentUser currentUser, ITicketEventRecorder events)
    : IRequestHandler<MergeTicketsCommand>
{
    public async Task Handle(MergeTicketsCommand request, CancellationToken cancellationToken)
    {
        var source = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        var target = await db.Tickets.Include(t => t.Status)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TargetTicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TargetTicketId);

        if (source.MergedIntoTicketId is not null)
        {
            throw new ConflictException($"Ticket {source.Number} is already merged and cannot be merged again.");
        }

        // Only the target needs the read-only guard: merging a source that is independently
        // terminal (e.g. cancelled) into another ticket is a reasonable cleanup action, but adding
        // messages to an already-closed target's thread is not.
        TicketReadOnlyGuard.EnsureEditable(target, target.Status.IsTerminal);

        if (source.CustomerId != target.CustomerId && !request.AllowCrossCustomer)
        {
            throw new ConflictException(
                $"Ticket {source.Number} and {target.Number} belong to different customers. " +
                "Set AllowCrossCustomer to merge them anyway.");
        }

        var terminalStatus = await db.TicketStatuses
            .Where(s => s.IsTerminal)
            .OrderBy(s => s.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No terminal ticket status is configured.");

        // Move messages with a bulk update rather than loading and re-saving each one: a busy thread
        // can have hundreds of rows, and none of their own fields need to change besides the FK.
        await db.TicketMessages
            .Where(m => m.TicketId == source.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.TicketId, target.Id), cancellationToken);

        source.MergedIntoTicketId = target.Id;
        source.StatusId = terminalStatus.Id;

        events.Record(source.Id, TicketEventType.Merged,
            field: nameof(Ticket.MergedIntoTicketId),
            newValue: target.Id.ToString(), newDisplay: target.Number,
            metadataJson: request.Reason is null ? null : System.Text.Json.JsonSerializer.Serialize(new { request.Reason }));

        events.Record(target.Id, TicketEventType.Merged,
            field: nameof(Ticket.MergedIntoTicketId),
            oldValue: source.Id.ToString(), oldDisplay: source.Number,
            metadataJson: request.Reason is null ? null : System.Text.Json.JsonSerializer.Serialize(new { request.Reason }));

        await db.SaveChangesAsync(cancellationToken);
    }
}
