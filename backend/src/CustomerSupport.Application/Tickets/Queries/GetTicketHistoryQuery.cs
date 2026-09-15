using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>
/// Keyset-paged read of one ticket's event timeline (Ticket Management / Ticket history), same
/// cursor shape as <see cref="Customers.Queries.GetInteractionsQuery"/>. Automation entries
/// (<c>IsSystemGenerated</c>) are excluded unless <see cref="IncludeSystem"/> is set — routine
/// automation would otherwise drown the human actions agents are looking for.
/// </summary>
[RequirePermission(Permissions.Tickets.ViewHistory)]
public record GetTicketHistoryQuery : IRequest<TicketHistoryPageDto>
{
    public Guid TicketId { get; init; }

    /// <summary>Keyset cursor: return rows strictly older than this pair. Omit for the first page.</summary>
    public DateTimeOffset? Before { get; init; }
    public Guid? BeforeId { get; init; }

    public IReadOnlyCollection<TicketEventType>? EventTypes { get; init; }
    public bool IncludeSystem { get; init; }

    public int PageSize { get; init; } = 50;
}

public class GetTicketHistoryQueryValidator : AbstractValidator<GetTicketHistoryQuery>
{
    public GetTicketHistoryQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.BeforeId).NotEmpty()
            .When(x => x.Before is not null)
            .WithMessage("BeforeId is required alongside Before, to break ties within the same instant.");
    }
}

public class GetTicketHistoryQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetTicketHistoryQuery, TicketHistoryPageDto>
{
    public async Task<TicketHistoryPageDto> Handle(GetTicketHistoryQuery request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.TicketId);
        }

        var query = db.TicketEvents.AsNoTracking().Where(e => e.TicketId == request.TicketId);

        if (!request.IncludeSystem)
        {
            query = query.Where(e => !e.IsSystemGenerated);
        }

        if (request.EventTypes is { Count: > 0 } eventTypes)
        {
            query = query.Where(e => eventTypes.Contains(e.EventType));
        }

        if (request.Before is { } before)
        {
            var beforeId = request.BeforeId!.Value;
            query = query.Where(e => e.OccurredAt < before || (e.OccurredAt == before && e.Id < beforeId));
        }

        var rows = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(request.PageSize + 1) // one extra row tells us whether more exist, with no COUNT(*)
            .Select(e => new TicketEventDto
            {
                Id = e.Id,
                TicketId = e.TicketId,
                EventType = e.EventType,
                ActorId = e.ActorId,
                ActorDisplayName = e.ActorDisplayName,
                IsSystemGenerated = e.IsSystemGenerated,
                Field = e.Field,
                OldValue = e.OldValue,
                NewValue = e.NewValue,
                OldDisplayValue = e.OldDisplayValue,
                NewDisplayValue = e.NewDisplayValue,
                MetadataJson = e.MetadataJson,
                TriggeredByRule = e.TriggeredByRule,
                OccurredAt = e.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > request.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new TicketHistoryPageDto { Items = rows, HasMore = hasMore };
    }
}
