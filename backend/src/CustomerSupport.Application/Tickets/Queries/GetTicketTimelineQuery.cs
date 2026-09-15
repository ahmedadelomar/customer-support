using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>
/// Keyset-paged, database-merged view of a ticket's events and messages in one chronological
/// stream — what the Conversation tab interleaves inline and a future "everything" view would
/// render directly. The merge happens as a SQL <c>UNION ALL</c> (via <see cref="Queryable.Concat{TSource}"/>
/// on two same-shaped projections), never by fetching both sets fully and merging in memory — a
/// long-lived ticket can carry thousands of rows.
/// </summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetTicketTimelineQuery : IRequest<TicketTimelinePageDto>
{
    public Guid TicketId { get; init; }

    public DateTimeOffset? Before { get; init; }
    public Guid? BeforeId { get; init; }

    public int PageSize { get; init; } = 50;
}

public class GetTicketTimelineQueryValidator : AbstractValidator<GetTicketTimelineQuery>
{
    public GetTicketTimelineQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.BeforeId).NotEmpty()
            .When(x => x.Before is not null)
            .WithMessage("BeforeId is required alongside Before, to break ties within the same instant.");
    }
}

public class GetTicketTimelineQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetTicketTimelineQuery, TicketTimelinePageDto>
{
    public async Task<TicketTimelinePageDto> Handle(GetTicketTimelineQuery request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.TicketId);
        }

        var events = db.TicketEvents.AsNoTracking()
            .Where(e => e.TicketId == request.TicketId)
            .Select(e => new TicketTimelineEntryDto
            {
                Kind = "event",
                Id = e.Id,
                OccurredAt = e.OccurredAt,
                EventType = e.EventType,
                ActorId = e.ActorId,
                ActorDisplayName = e.ActorDisplayName,
                IsSystemGenerated = e.IsSystemGenerated,
                OldDisplayValue = e.OldDisplayValue,
                NewDisplayValue = e.NewDisplayValue,
                MetadataJson = e.MetadataJson,
                TriggeredByRule = e.TriggeredByRule,
                Channel = null,
                Direction = null,
                AuthorType = null,
                Subject = null,
                BodyText = null,
                IsInternalNote = null,
            });

        var messages = db.TicketMessages.AsNoTracking()
            .Where(m => m.TicketId == request.TicketId && !m.IsDeleted);

        // A portal login (CustomerId set) never sees internal notes on the merged timeline, even
        // though no portal endpoint calls this yet — the filter belongs to the query, not to
        // whichever caller happens to exist today.
        if (currentUser.CustomerId is not null)
        {
            messages = messages.Where(m => !m.IsInternalNote);
        }

        var messageEntries = messages.Select(m => new TicketTimelineEntryDto
        {
            Kind = "message",
            Id = m.Id,
            OccurredAt = m.SentAt,
            EventType = null,
            ActorId = m.AuthorId,
            ActorDisplayName = m.AuthorDisplayName,
            IsSystemGenerated = null,
            OldDisplayValue = null,
            NewDisplayValue = null,
            MetadataJson = null,
            TriggeredByRule = null,
            Channel = m.Channel,
            Direction = m.Direction,
            AuthorType = m.AuthorType,
            Subject = m.Subject,
            BodyText = m.BodyText,
            IsInternalNote = m.IsInternalNote,
        });

        var merged = events.Concat(messageEntries);

        if (request.Before is { } before)
        {
            var beforeId = request.BeforeId!.Value;
            merged = merged.Where(x => x.OccurredAt < before || (x.OccurredAt == before && x.Id < beforeId));
        }

        var rows = await merged
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > request.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new TicketTimelinePageDto { Items = rows, HasMore = hasMore };
    }
}
