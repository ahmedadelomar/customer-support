using CustomerSupport.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Infrastructure.Identity;

/// <summary>
/// A role is a named bundle of permissions. Permissions themselves live in
/// <c>RolePermission</c>, so the grant matrix is data rather than code.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>, IAuditable
{
    public LocalizedText DisplayName { get; set; } = new();
    public string? Description { get; set; }

    /// <summary>System roles cannot be deleted or renamed; their permissions can still be edited.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Restricts the role to one branch. Null means it may be granted in any branch.</summary>
    public Guid? BranchId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
