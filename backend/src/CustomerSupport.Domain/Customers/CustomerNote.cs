using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Customers;

/// <summary>
/// Free-text agent note on a customer (Customer Management / Notes and attachments).
/// Files live in <c>Attachment</c> rows with <c>OwnerType = "CustomerNote"</c>.
/// </summary>
public class CustomerNote : BaseEntity, IAuditable, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    /// <summary>Set when the note was written while working a specific ticket.</summary>
    public Guid? TicketId { get; set; }

    public string Body { get; set; } = string.Empty;
    /// <summary>Pinned notes surface at the top of the customer panel in the agent workspace.</summary>
    public bool IsPinned { get; set; }
    /// <summary>Internal notes are never exposed through the customer portal.</summary>
    public bool IsInternal { get; set; } = true;
    public int AttachmentCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
