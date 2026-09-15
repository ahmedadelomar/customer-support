using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Tickets.Dtos;

/// <summary>
/// One row of the ticket timeline (Ticket Management / Ticket history). Display values are copied
/// verbatim from <see cref="Domain.Tickets.TicketEvent"/> — never re-resolved from current lookup
/// rows — so a later rename of a status or category name never rewrites past history.
/// </summary>
public record TicketEventDto
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public TicketEventType EventType { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorDisplayName { get; init; }
    public bool IsSystemGenerated { get; init; }
    public string? Field { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? OldDisplayValue { get; init; }
    public string? NewDisplayValue { get; init; }
    public string? MetadataJson { get; init; }
    public string? TriggeredByRule { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// Keyset-paged result, same reasoning as <see cref="Customers.Dtos.InteractionPageDto"/>: an
/// append-only timeline read newest-first has no stable "total" worth computing.
/// </summary>
public record TicketHistoryPageDto
{
    public IReadOnlyList<TicketEventDto> Items { get; init; } = Array.Empty<TicketEventDto>();
    public bool HasMore { get; init; }
}

/// <summary>
/// One row of the merged event+message timeline. <c>Kind</c> selects which subset of the optional
/// fields is populated — "event" fields mirror <see cref="TicketEventDto"/>, "message" fields mirror
/// <see cref="TicketMessageDto"/>. Kept as one flat shape (rather than two DTOs behind a union type)
/// because the source query is a database-level <c>UNION ALL</c> that must project both sides to the
/// exact same shape for EF Core to translate it.
/// </summary>
public record TicketTimelineEntryDto
{
    public string Kind { get; init; } = "event";
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    // Event fields (Kind == "event").
    public TicketEventType? EventType { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorDisplayName { get; init; }
    public bool? IsSystemGenerated { get; init; }
    public string? OldDisplayValue { get; init; }
    public string? NewDisplayValue { get; init; }
    public string? MetadataJson { get; init; }
    public string? TriggeredByRule { get; init; }

    // Message fields (Kind == "message").
    public ChannelKey? Channel { get; init; }
    public MessageDirection? Direction { get; init; }
    public MessageAuthorType? AuthorType { get; init; }
    public string? Subject { get; init; }
    public string? BodyText { get; init; }
    public bool? IsInternalNote { get; init; }
}

public record TicketTimelinePageDto
{
    public IReadOnlyList<TicketTimelineEntryDto> Items { get; init; } = Array.Empty<TicketTimelineEntryDto>();
    public bool HasMore { get; init; }
}
