namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>One row in the caller's mentions inbox.</summary>
public record MentionDto
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string TicketSubject { get; init; } = string.Empty;
    public Guid MentionedById { get; init; }
    public string MentionedByNameEn { get; init; } = string.Empty;
    public string MentionedByNameAr { get; init; } = string.Empty;
    public string ExcerptEn { get; init; } = string.Empty;
    public DateTimeOffset MentionedAt { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
}

/// <summary>
/// A colleague candidate for the "@" picker. <see cref="CanView"/> is false when the candidate would
/// not currently see the ticket via <c>WhereTicketVisible</c> — still returned, never excluded, so the
/// editor can warn instead of silently narrowing the list (per the story's own rule).
/// </summary>
public record MentionableUserDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool CanView { get; init; }
}

/// <summary>One row on the ticket's watchers list.</summary>
public record WatcherDto
{
    public Guid UserId { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool AddedByAutomation { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public bool IsCurrentUser { get; init; }
}
