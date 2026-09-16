using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Identity-backed implementation of <see cref="IRoleAdminService"/>. The permission grants
/// themselves live on <c>RolePermission</c>, which IS exposed through <c>IAppDbContext</c>, so only
/// the role records are managed here.
/// </summary>
public class RoleAdminService(AppDbContext db, RoleManager<ApplicationRole> roleManager) : IRoleAdminService
{
    public async Task<IReadOnlyList<RoleSummaryDto>> ListAsync(CancellationToken ct = default) =>
        await db.Roles.AsNoTracking()
            .OrderBy(r => r.DisplayName.En)
            .Select(r => new RoleSummaryDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                DisplayNameEn = r.DisplayName.En,
                DisplayNameAr = r.DisplayName.Ar,
                Description = r.Description,
                IsSystem = r.IsSystem,
            })
            .ToListAsync(ct);

    public async Task<RoleSummaryDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Roles.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RoleSummaryDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                DisplayNameEn = r.DisplayName.En,
                DisplayNameAr = r.DisplayName.Ar,
                Description = r.Description,
                IsSystem = r.IsSystem,
            })
            .FirstOrDefaultAsync(ct);

    public async Task<Guid> CreateAsync(
        string name, LocalizedText displayName, string? description, CancellationToken ct = default)
    {
        var role = new ApplicationRole
        {
            Name = name,
            DisplayName = displayName,
            Description = description,

            // Only the seeder creates system roles; anything made through the API is a custom role
            // and therefore renameable and deletable.
            IsSystem = false,
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        return role.Id;
    }

    public async Task RenameAsync(
        Guid id, LocalizedText displayName, string? description, CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Role", id);

        if (role.IsSystem)
        {
            throw new ConflictException("System roles cannot be renamed. Their permissions can still be edited.");
        }

        role.DisplayName = displayName;
        role.Description = description;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Role", id);

        if (role.IsSystem)
        {
            throw new ConflictException("System roles cannot be deleted.");
        }

        var holders = await db.UserRoles.CountAsync(ur => ur.RoleId == id, ct);
        if (holders > 0)
        {
            throw new ConflictException(
                $"This role is still held by {holders} user(s). Reassign them before deleting it.");
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) =>
        await db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);
}
