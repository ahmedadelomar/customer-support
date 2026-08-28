using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Identity;

/// <summary>
/// Typed key/value configuration editable at runtime (Security &amp; Administration / System configuration).
/// A branch-scoped row overrides the global row with the same <see cref="Key"/>.
/// </summary>
public class SystemSetting : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    /// <summary>One of <c>string</c>, <c>int</c>, <c>bool</c>, <c>json</c>, <c>secret</c>.</summary>
    public string DataType { get; set; } = "string";
    public string Category { get; set; } = "General";
    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    /// <summary>Secrets are encrypted at rest and redacted in API responses and audit logs.</summary>
    public bool IsSecret { get; set; }
    /// <summary>System settings are visible to admins but cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
