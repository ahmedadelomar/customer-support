using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Queries;

/// <summary>
/// Keyset-paged read of one customer's interaction timeline. Never returns an item this story (or
/// any other) can mutate — see the class remark on <see cref="Domain.Customers.Interaction"/>: it is
/// a read-optimised projection, written only by <see cref="IInteractionRecorder"/>.
/// </summary>
[RequirePermission(Permissions.Customers.ViewHistory)]
public record GetInteractionsQuery : IRequest<InteractionPageDto>
{
    public Guid CustomerId { get; init; }

    /// <summary>Keyset cursor: return rows strictly older than this pair. Omit for the first page.</summary>
    public DateTimeOffset? Before { get; init; }
    public Guid? BeforeId { get; init; }

    public ChannelKey? Channel { get; init; }
    public MessageDirection? Direction { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    public int PageSize { get; init; } = 30;
}

public class GetInteractionsQueryValidator : AbstractValidator<GetInteractionsQuery>
{
    public GetInteractionsQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.BeforeId).NotEmpty()
            .When(x => x.Before is not null)
            .WithMessage("BeforeId is required alongside Before, to break ties within the same instant.");
    }
}

public class GetInteractionsQueryHandler(IAppDbContext db, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetInteractionsQuery, InteractionPageDto>
{
    public async Task<InteractionPageDto> Handle(GetInteractionsQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Customers.Customer), request.CustomerId);
        }

        var query = db.Interactions.AsNoTracking().Where(i => i.CustomerId == request.CustomerId);

        if (request.Channel is { } channel) query = query.Where(i => i.Channel == channel);
        if (request.Direction is { } direction) query = query.Where(i => i.Direction == direction);
        if (request.From is { } from) query = query.Where(i => i.OccurredAt >= from);
        if (request.To is { } to) query = query.Where(i => i.OccurredAt <= to);

        if (request.Before is { } before)
        {
            var beforeId = request.BeforeId!.Value;

            // Strictly older than the last row of the previous page. The Id tiebreak is what makes
            // this safe under concurrent inserts at the same instant — offset paging has no
            // equivalent and can skip or repeat rows when new entries land mid-scroll.
            query = query.Where(i => i.OccurredAt < before || (i.OccurredAt == before && i.Id < beforeId));
        }

        var rows = await query
            .OrderByDescending(i => i.OccurredAt)
            .ThenByDescending(i => i.Id)
            .Take(request.PageSize + 1) // one extra row tells us whether more exist, with no COUNT(*)
            .Select(i => new
            {
                i.Id,
                i.TicketId,
                i.Channel,
                i.Direction,
                i.Subject,
                i.Preview,
                i.AgentId,
                i.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > request.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        // TicketId and AgentId are loose references (see the Interaction class remark), not FK
        // navigations, so both are resolved as a second, batched lookup rather than a join.
        var ticketIds = rows.Where(r => r.TicketId is not null).Select(r => r.TicketId!.Value).Distinct().ToList();
        var ticketNumbers = ticketIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Tickets.AsNoTracking()
                .Where(t => ticketIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Number })
                .ToDictionaryAsync(t => t.Id, t => t.Number, cancellationToken);

        var agentIds = rows.Where(r => r.AgentId is not null).Select(r => r.AgentId!.Value).Distinct();
        var agentNames = await userNames.ResolveAsync(agentIds, cancellationToken);

        var items = rows.Select(row => new InteractionDto
        {
            Id = row.Id,
            TicketId = row.TicketId,
            TicketNumber = row.TicketId is { } ticketId && ticketNumbers.TryGetValue(ticketId, out var number)
                ? number
                : null,
            Channel = row.Channel,
            Direction = row.Direction,
            Subject = row.Subject,
            Preview = row.Preview,
            AgentId = row.AgentId,
            AgentNameEn = row.AgentId is { } agentId && agentNames.TryGetValue(agentId, out var name)
                ? name.En
                : null,
            AgentNameAr = row.AgentId is { } agentId2 && agentNames.TryGetValue(agentId2, out var name2)
                ? name2.Ar
                : null,
            OccurredAt = row.OccurredAt,
        }).ToList();

        return new InteractionPageDto { Items = items, HasMore = hasMore };
    }
}
