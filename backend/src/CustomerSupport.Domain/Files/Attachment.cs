using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Files;

/// <summary>
/// Polymorphic file record shared by customer notes, ticket messages and KB articles.
/// <see cref="OwnerType"/>/<see cref="OwnerId"/> are a loose reference (no FK) so any aggregate can attach files.
/// </summary>
public class Attachment : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    /// <summary>Discriminator: <c>Customer</c>, <c>CustomerNote</c>, <c>Ticket</c>, <c>TicketMessage</c>, <c>KbArticle</c>, …</summary>
    public string OwnerType { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    /// <summary>Provider-relative key (local disk path or blob key) — never a public URL.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    /// <summary>True for files a customer may download from the portal.</summary>
    public bool IsPublic { get; set; }
    public string? ScanResult { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
