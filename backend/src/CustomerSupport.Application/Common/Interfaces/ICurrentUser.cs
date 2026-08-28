namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// The authenticated principal for the current request, resolved from the JWT.
/// Handlers depend on this rather than on HttpContext so they stay testable.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }

    /// <summary>Branch the user is acting in. Scopes every tenant-aware query.</summary>
    Guid? BranchId { get; }

    /// <summary>Branches the user may access. Empty means every branch (system administrator).</summary>
    IReadOnlyCollection<Guid> AccessibleBranchIds { get; }
    IReadOnlyCollection<Guid> DepartmentIds { get; }

    /// <summary>Permission keys granted through the roles held, for example <c>tickets.assign</c>.</summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>Set when the caller is a customer-portal login rather than an agent.</summary>
    Guid? CustomerId { get; }

    bool IsAuthenticated { get; }
    bool HasPermission(string permissionKey);
}
