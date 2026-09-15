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
/// Edits subject, description, category, priority and tags. Assignment, status and department moves
/// go through their own endpoints (assignment/CS-502, status workflow, transfer/CS-1203) because each
/// carries side effects this generic edit must not trigger.
/// </summary>
[RequirePermission(Permissions.Tickets.Update)]
public record UpdateTicketCommand : IRequest
{
    public Guid Id { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public Guid PriorityId { get; init; }
    public IReadOnlyList<Guid> TagIds { get; init; } = Array.Empty<Guid>();
}

public class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PriorityId).NotEmpty();
    }
}

public class UpdateTicketCommandHandler(IAppDbContext db, ITicketEventRecorder events, IDateTimeProvider clock)
    : IRequestHandler<UpdateTicketCommand>
{
    public async Task Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.Id);

        if (ticket.CategoryId != request.CategoryId)
        {
            var oldCategory = await db.TicketCategories.FirstAsync(c => c.Id == ticket.CategoryId, cancellationToken);
            var newCategory = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
                ?? throw new NotFoundException(nameof(TicketCategory), request.CategoryId);

            events.Record(ticket.Id, TicketEventType.CategoryChanged,
                field: nameof(Ticket.CategoryId),
                oldValue: oldCategory.Id.ToString(), newValue: newCategory.Id.ToString(),
                oldDisplay: oldCategory.Name.En, newDisplay: newCategory.Name.En);

            ticket.CategoryId = newCategory.Id;
        }

        if (ticket.PriorityId != request.PriorityId)
        {
            var oldPriority = await db.TicketPriorities.FirstAsync(p => p.Id == ticket.PriorityId, cancellationToken);
            var newPriority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.Id == request.PriorityId, cancellationToken)
                ?? throw new NotFoundException(nameof(TicketPriority), request.PriorityId);

            events.Record(ticket.Id, TicketEventType.PriorityChanged,
                field: nameof(Ticket.PriorityId),
                oldValue: oldPriority.Id.ToString(), newValue: newPriority.Id.ToString(),
                oldDisplay: oldPriority.Name.En, newDisplay: newPriority.Name.En);

            ticket.PriorityId = newPriority.Id;
        }

        ticket.Subject = request.Subject;
        ticket.Description = request.Description;

        var oldTagIds = ticket.Tags.Select(t => t.TagId).OrderBy(id => id).ToList();
        var newTagIds = request.TagIds.Distinct().OrderBy(id => id).ToList();

        if (!oldTagIds.SequenceEqual(newTagIds))
        {
            var toRemove = ticket.Tags.Where(t => !newTagIds.Contains(t.TagId)).ToList();
            foreach (var tag in toRemove)
            {
                ticket.Tags.Remove(tag);
            }

            var toAddIds = newTagIds.Except(oldTagIds).ToList();
            foreach (var tagId in toAddIds)
            {
                ticket.Tags.Add(new TicketTag { TicketId = ticket.Id, TagId = tagId, TaggedAt = clock.UtcNow });
            }

            events.Record(ticket.Id, TicketEventType.TagsChanged,
                field: nameof(Ticket.Tags),
                metadataJson: JsonSerializer.Serialize(new { Old = oldTagIds, New = newTagIds }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
