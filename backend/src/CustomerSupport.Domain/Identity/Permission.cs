using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Identity;

/// <summary>
/// A single grantable action, addressed as <c>{Category}.{Action}</c> (e.g. <c>Tickets.Assign</c>).
/// Seeded from <c>PermissionRegistry</c>; roles are composed from these rows.
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>Stable key used in code and in the JWT <c>perm</c> claim, e.g. <c>tickets.assign</c>.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Grouping shown in the role editor, e.g. <c>Tickets</c>.</summary>
    public string Category { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    /// <summary>System permissions cannot be deleted, only granted or revoked.</summary>
    public bool IsSystem { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
