using CustomerSupport.Application.Common.Models;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>One row of the administration users list.</summary>
public record UserListItemDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public UserType UserType { get; init; }
    public Guid? BranchId { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }
    public string AvailabilityStatus { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public IReadOnlyList<RoleSummaryDto> Roles { get; init; } = [];
}

/// <summary>A user's full record, for the edit form.</summary>
public record UserDetailDto : UserListItemDto
{
    public IReadOnlyList<Guid> AccessibleBranchIds { get; init; } = [];
    public string PreferredLanguage { get; init; } = "ar";
    public string? TimeZoneId { get; init; }
    public int MaxConcurrentTickets { get; init; }
}

/// <summary>A role as shown in the user form's picker and the roles list.</summary>
public record RoleSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
}

/// <summary>Filter for <see cref="IUserAdminService.ListAsync"/>, carrying the paging the handler already validated.</summary>
public record UserListFilter
{
    public string? Search { get; init; }
    public Guid? RoleId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? BranchId { get; init; }
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>Fields an administrator may set when creating an account.</summary>
public record CreateUserRequest
{
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string Password { get; init; } = string.Empty;
    public LocalizedText DisplayName { get; init; } = new();
    public string? JobTitle { get; init; }
    public Guid? BranchId { get; init; }
    public IReadOnlyList<Guid> AccessibleBranchIds { get; init; } = [];
    public Guid? DepartmentId { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public string? TimeZoneId { get; init; }
    public int MaxConcurrentTickets { get; init; }
    public IReadOnlyList<Guid> RoleIds { get; init; } = [];
}

/// <summary>Fields an administrator may change on an existing account. The username is immutable.</summary>
public record UpdateUserRequest
{
    public string? Email { get; init; }
    public LocalizedText DisplayName { get; init; } = new();
    public string? JobTitle { get; init; }
    public Guid? BranchId { get; init; }
    public IReadOnlyList<Guid> AccessibleBranchIds { get; init; } = [];
    public Guid? DepartmentId { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public string? TimeZoneId { get; init; }
    public int MaxConcurrentTickets { get; init; }
}

/// <summary>
/// Administration of agent accounts. Exists for the same reason <see cref="IAgentDirectory"/> does:
/// <c>ApplicationUser</c> and ASP.NET Identity live in Infrastructure and are deliberately not exposed
/// through <see cref="IAppDbContext"/>, so Application-layer handlers reach them through this contract
/// instead. Handlers keep the permission attributes, validation and product rules; this only does the
/// Identity-specific data access.
/// </summary>
public interface IUserAdminService
{
    Task<PagedResult<UserListItemDto>> ListAsync(UserListFilter filter, CancellationToken ct = default);
    Task<UserDetailDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates the account with <c>MustChangePassword</c> set, and assigns its roles.</summary>
    Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>Deactivating also revokes every refresh token, so access ends at once rather than at token expiry.</summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task ReplaceRolesAsync(Guid id, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);
    Task SetAvailabilityAsync(Guid id, string availabilityStatus, CancellationToken ct = default);

    /// <summary>Changes the caller's own password and clears <c>MustChangePassword</c>.</summary>
    Task ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Ids of users currently holding <paramref name="roleId"/> — used by the role-deletion guard.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsInRoleAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Role ids held by one user, for the self-lockout guard on permission edits.</summary>
    Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Active user count per branch id, for the branch list and its deactivation guard.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByBranchAsync(CancellationToken ct = default);
}

/// <summary>
/// Administration of roles. Split from <see cref="IUserAdminService"/> because role editing
/// (CS-1002) is its own screen and its own permission; same Infrastructure-boundary reasoning.
/// </summary>
public interface IRoleAdminService
{
    Task<IReadOnlyList<RoleSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<RoleSummaryDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(string name, LocalizedText displayName, string? description, CancellationToken ct = default);
    Task RenameAsync(Guid id, LocalizedText displayName, string? description, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default);
}
