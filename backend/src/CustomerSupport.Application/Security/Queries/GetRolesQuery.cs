using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Security.Queries;

/// <summary>A role with everything the roles list and the user form's picker need.</summary>
public record RoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public int UserCount { get; init; }
    public IReadOnlyList<Guid> PermissionIds { get; init; } = [];
    public IReadOnlyList<string> PermissionKeys { get; init; } = [];
}

/// <summary>
/// Every role with its granted permissions and how many users hold it (Security &amp; Administration /
/// Permissions). Gated on <c>admin.users.view</c> rather than <c>admin.roles.manage</c> because the
/// user form's role picker needs it too.
/// </summary>
[RequirePermission(Permissions.Administration.ViewUsers)]
public record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public class GetRolesQueryHandler(IAppDbContext db, IRoleAdminService roles)
    : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var summaries = await roles.ListAsync(cancellationToken);
        var roleIds = summaries.Select(r => r.Id).ToList();

        var grants = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new { rp.RoleId, rp.PermissionId, rp.Permission.Key })
            .ToListAsync(cancellationToken);

        var grantsByRole = grants.GroupBy(g => g.RoleId).ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<RoleDto>(summaries.Count);
        foreach (var role in summaries)
        {
            var held = grantsByRole.GetValueOrDefault(role.Id, []);

            results.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                DisplayNameEn = role.DisplayNameEn,
                DisplayNameAr = role.DisplayNameAr,
                Description = role.Description,
                IsSystem = role.IsSystem,
                UserCount = await roles.CountUsersInRoleAsync(role.Id, cancellationToken),
                PermissionIds = held.Select(h => h.PermissionId).ToList(),
                PermissionKeys = held.Select(h => h.Key).OrderBy(k => k).ToList(),
            });
        }

        return results;
    }
}
