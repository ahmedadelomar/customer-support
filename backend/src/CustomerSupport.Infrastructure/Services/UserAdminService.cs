using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Identity-backed implementation of <see cref="IUserAdminService"/>. Lives in Infrastructure for the
/// same reason <see cref="AgentDirectory"/> does: it is the only layer allowed to touch
/// <see cref="ApplicationUser"/> and <see cref="UserManager{TUser}"/>.
/// </summary>
public class UserAdminService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider clock) : IUserAdminService
{
    /// <summary>Columns a caller may sort by. Anything else falls back to display name.</summary>
    private static readonly string[] SortableColumns = ["username", "displayname", "email", "lastloginat", "createdat"];

    public async Task<PagedResult<UserListItemDto>> ListAsync(UserListFilter filter, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking().Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.UserName!, $"%{term}%") ||
                EF.Functions.Like(u.DisplayName.En, $"%{term}%") ||
                EF.Functions.Like(u.DisplayName.Ar, $"%{term}%") ||
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%")));
        }

        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(u => u.DepartmentId == departmentId);
        }

        if (filter.BranchId is { } branchId)
        {
            query = query.Where(u => u.BranchId == branchId);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (filter.RoleId is { } roleId)
        {
            var idsInRole = db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId);
            query = query.Where(u => idsInRole.Contains(u.Id));
        }

        var totalCount = await query.CountAsync(ct);

        var sortBy = (filter.SortBy ?? string.Empty).ToLowerInvariant();
        query = (SortableColumns.Contains(sortBy) ? sortBy : "displayname", filter.SortDescending) switch
        {
            ("username", false) => query.OrderBy(u => u.UserName),
            ("username", true) => query.OrderByDescending(u => u.UserName),
            ("email", false) => query.OrderBy(u => u.Email),
            ("email", true) => query.OrderByDescending(u => u.Email),
            ("lastloginat", false) => query.OrderBy(u => u.LastLoginAt),
            ("lastloginat", true) => query.OrderByDescending(u => u.LastLoginAt),
            ("createdat", false) => query.OrderBy(u => u.CreatedAt),
            ("createdat", true) => query.OrderByDescending(u => u.CreatedAt),
            (_, true) => query.OrderByDescending(u => u.DisplayName.En),
            _ => query.OrderBy(u => u.DisplayName.En),
        };

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.DisplayName,
                u.JobTitle,
                u.UserType,
                u.BranchId,
                u.DepartmentId,
                u.AvailabilityStatus,
                u.IsActive,
                u.MustChangePassword,
                u.LastLoginAt,
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var rolesByUser = await RolesByUserAsync(ids, ct);
        var (branches, departments) = await ScopeNamesAsync(
            rows.Select(r => r.BranchId), rows.Select(r => r.DepartmentId), ct);

        var items = rows.Select(r => new UserListItemDto
        {
            Id = r.Id,
            UserName = r.UserName ?? string.Empty,
            Email = r.Email,
            DisplayNameEn = r.DisplayName.En,
            DisplayNameAr = r.DisplayName.Ar,
            JobTitle = r.JobTitle,
            UserType = r.UserType,
            BranchId = r.BranchId,
            BranchNameEn = r.BranchId is { } b && branches.TryGetValue(b, out var bn) ? bn.En : null,
            BranchNameAr = r.BranchId is { } b2 && branches.TryGetValue(b2, out var bn2) ? bn2.Ar : null,
            DepartmentId = r.DepartmentId,
            DepartmentNameEn = r.DepartmentId is { } d && departments.TryGetValue(d, out var dn) ? dn.En : null,
            DepartmentNameAr = r.DepartmentId is { } d2 && departments.TryGetValue(d2, out var dn2) ? dn2.Ar : null,
            AvailabilityStatus = r.AvailabilityStatus,
            IsActive = r.IsActive,
            MustChangePassword = r.MustChangePassword,
            LastLoginAt = r.LastLoginAt,
            Roles = rolesByUser.GetValueOrDefault(r.Id, []),
        }).ToList();

        return PagedResult<UserListItemDto>.Create(items, filter.Page, filter.PageSize, totalCount);
    }

    public async Task<UserDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null)
        {
            return null;
        }

        var rolesByUser = await RolesByUserAsync([id], ct);
        var (branches, departments) = await ScopeNamesAsync([user.BranchId], [user.DepartmentId], ct);

        return new UserDetailDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            DisplayNameEn = user.DisplayName.En,
            DisplayNameAr = user.DisplayName.Ar,
            JobTitle = user.JobTitle,
            UserType = user.UserType,
            BranchId = user.BranchId,
            BranchNameEn = user.BranchId is { } b && branches.TryGetValue(b, out var bn) ? bn.En : null,
            BranchNameAr = user.BranchId is { } b2 && branches.TryGetValue(b2, out var bn2) ? bn2.Ar : null,
            DepartmentId = user.DepartmentId,
            DepartmentNameEn = user.DepartmentId is { } d && departments.TryGetValue(d, out var dn) ? dn.En : null,
            DepartmentNameAr = user.DepartmentId is { } d2 && departments.TryGetValue(d2, out var dn2) ? dn2.Ar : null,
            AvailabilityStatus = user.AvailabilityStatus,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            LastLoginAt = user.LastLoginAt,
            Roles = rolesByUser.GetValueOrDefault(id, []),
            AccessibleBranchIds = ParseBranchIds(user.AccessibleBranchIds),
            PreferredLanguage = user.PreferredLanguage,
            TimeZoneId = user.TimeZoneId,
            MaxConcurrentTickets = user.MaxConcurrentTickets,
        };
    }

    public async Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = request.DisplayName,
            JobTitle = request.JobTitle,
            BranchId = request.BranchId,
            AccessibleBranchIds = FormatBranchIds(request.AccessibleBranchIds),
            DepartmentId = request.DepartmentId,
            PreferredLanguage = request.PreferredLanguage,
            TimeZoneId = request.TimeZoneId,
            MaxConcurrentTickets = request.MaxConcurrentTickets,
            IsActive = true,

            // An administrator-chosen password is a shared secret until its owner replaces it.
            MustChangePassword = true,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        ThrowIfFailed(created);

        var roleNames = await RoleNamesAsync(request.RoleIds, ct);
        if (roleNames.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["roleIds"] = ["A user must hold at least one role."],
            });
        }

        ThrowIfFailed(await userManager.AddToRolesAsync(user, roleNames));
        return user.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("User", id);

        user.Email = request.Email;
        user.DisplayName = request.DisplayName;
        user.JobTitle = request.JobTitle;
        user.BranchId = request.BranchId;
        user.AccessibleBranchIds = FormatBranchIds(request.AccessibleBranchIds);
        user.DepartmentId = request.DepartmentId;
        user.PreferredLanguage = request.PreferredLanguage;
        user.TimeZoneId = request.TimeZoneId;
        user.MaxConcurrentTickets = request.MaxConcurrentTickets;

        ThrowIfFailed(await userManager.UpdateAsync(user));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("User", id);

        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));

        if (!isActive)
        {
            // Without this the account keeps working until its access token expires — up to an hour
            // of activity after an administrator believed they had cut it off.
            await db.Set<RefreshToken>()
                .Where(t => t.UserId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.RevokedAt, clock.UtcNow)
                          .SetProperty(t => t.RevokedReason, "Account deactivated"),
                    ct);
        }
    }

    public async Task ReplaceRolesAsync(Guid id, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("User", id);

        var target = await RoleNamesAsync(roleIds, ct);
        if (target.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["roleIds"] = ["A user must hold at least one role."],
            });
        }

        var current = await userManager.GetRolesAsync(user);

        var toRemove = current.Except(target, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = target.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0)
        {
            ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, toRemove));
        }

        if (toAdd.Count > 0)
        {
            ThrowIfFailed(await userManager.AddToRolesAsync(user, toAdd));
        }
    }

    public async Task SetAvailabilityAsync(Guid id, string availabilityStatus, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("User", id);

        user.AvailabilityStatus = availabilityStatus;
        ThrowIfFailed(await userManager.UpdateAsync(user));
    }

    public async Task ChangePasswordAsync(
        Guid id, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("User", id);

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            // Identity reports a wrong current password as PasswordMismatch; surface it on the field
            // the user can actually fix rather than as a generic failure.
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = result.Errors.Any(e => e.Code == "PasswordMismatch")
                    ? ["The current password is incorrect."]
                    : [],
                ["newPassword"] = result.Errors.Where(e => e.Code != "PasswordMismatch")
                    .Select(e => e.Description).ToArray(),
            });
        }

        user.MustChangePassword = false;
        ThrowIfFailed(await userManager.UpdateAsync(user));
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsInRoleAsync(Guid roleId, CancellationToken ct = default) =>
        await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByBranchAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted && u.BranchId != null)
            .GroupBy(u => u.BranchId!.Value)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, ct);

    private async Task<Dictionary<Guid, IReadOnlyList<RoleSummaryDto>>> RolesByUserAsync(
        IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var pairs = await db.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new
            {
                ur.UserId,
                Role = new RoleSummaryDto
                {
                    Id = r.Id,
                    Name = r.Name ?? string.Empty,
                    DisplayNameEn = r.DisplayName.En,
                    DisplayNameAr = r.DisplayName.Ar,
                    Description = r.Description,
                    IsSystem = r.IsSystem,
                },
            })
            .ToListAsync(ct);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RoleSummaryDto>)g.Select(p => p.Role).ToList());
    }

    private async Task<(Dictionary<Guid, Domain.Common.LocalizedText> Branches,
                        Dictionary<Guid, Domain.Common.LocalizedText> Departments)> ScopeNamesAsync(
        IEnumerable<Guid?> branchIds, IEnumerable<Guid?> departmentIds, CancellationToken ct)
    {
        var bIds = branchIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var dIds = departmentIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();

        var branches = bIds.Count == 0
            ? []
            : await db.Branches.AsNoTracking().Where(b => bIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var departments = dIds.Count == 0
            ? []
            : await db.Departments.AsNoTracking().Where(d => dIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return (branches, departments);
    }

    private async Task<List<string>> RoleNamesAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        return await db.Roles
            .Where(r => roleIds.Contains(r.Id) && r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync(ct);
    }

    private static List<Guid> ParseBranchIds(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
                 .Where(id => id is not null)
                 .Select(id => id!.Value)
                 .ToList();

    private static string? FormatBranchIds(IReadOnlyList<Guid> ids) =>
        ids.Count == 0 ? null : string.Join(',', ids);

    /// <summary>Turns an Identity failure into the same field-level 400 the rest of the API returns.</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new ValidationException(new Dictionary<string, string[]>
        {
            [""] = result.Errors.Select(e => e.Description).ToArray(),
        });
    }
}
