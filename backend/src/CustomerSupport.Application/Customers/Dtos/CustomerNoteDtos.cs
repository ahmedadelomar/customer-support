using CustomerSupport.Application.Files.Dtos;

namespace CustomerSupport.Application.Customers.Dtos;

/// <summary>One note on a customer profile, with its attachments and the caller's own edit rights resolved.</summary>
public record CustomerNoteDto
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? TicketId { get; init; }
    public string Body { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
    public bool IsInternal { get; init; }
    public Guid? CreatedById { get; init; }
    public string? AuthorNameEn { get; init; }
    public string? AuthorNameAr { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>
    /// Resolved server-side (author, or holds <c>customers.notes.manage</c>) so the UI shows or
    /// hides Edit/Delete without duplicating the rule the handlers already enforce.
    /// </summary>
    public bool CanEdit { get; init; }

    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = [];
}
