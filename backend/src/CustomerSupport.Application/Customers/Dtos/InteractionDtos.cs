using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Customers.Dtos;

/// <summary>One entry on the unified customer timeline.</summary>
public record InteractionDto
{
    public Guid Id { get; init; }
    public Guid? TicketId { get; init; }
    /// <summary>The ticket's human-readable number, resolved for display — null once the source ticket is gone.</summary>
    public string? TicketNumber { get; init; }
    public ChannelKey Channel { get; init; }
    public MessageDirection Direction { get; init; }
    public string? Subject { get; init; }
    public string? Preview { get; init; }
    public Guid? AgentId { get; init; }
    public string? AgentNameEn { get; init; }
    public string? AgentNameAr { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// Keyset-paged result. Deliberately not <c>PagedResult&lt;T&gt;</c> (which is offset-based, with a
/// total count) — a timeline that only ever grows and is read newest-first has no stable "total"
/// worth computing, and offset paging is exactly what keyset pagination exists to avoid.
/// </summary>
public record InteractionPageDto
{
    public IReadOnlyList<InteractionDto> Items { get; init; } = Array.Empty<InteractionDto>();
    public bool HasMore { get; init; }
}
