using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Identity;

/// <summary>
/// Append-only audit trail. Written by the <c>AuditSaveChangesInterceptor</c> for entity mutations
/// and explicitly by the auth/export pipelines. Never updated or deleted by application code.
/// </summary>
public class AuditLog : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public AuditAction Action { get; set; }
    /// <summary>CLR entity name, e.g. <c>Ticket</c>.</summary>
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    /// <summary>JSON of the changed properties before the write; null for creates.</summary>
    public string? OldValues { get; set; }
    /// <summary>JSON of the changed properties after the write; null for deletes.</summary>
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    /// <summary>Correlates every audit row produced by one HTTP request.</summary>
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
