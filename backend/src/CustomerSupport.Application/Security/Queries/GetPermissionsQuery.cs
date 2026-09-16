using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Security.Queries;

/// <summary>One grantable permission, as shown in the role editor's matrix.</summary>
public record PermissionDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>One category panel in the matrix.</summary>
public record PermissionCategoryDto
{
    public string Category { get; init; } = string.Empty;
    public IReadOnlyList<PermissionDto> Permissions { get; init; } = [];
}

/// <summary>
/// The permission catalogue, grouped by category (Security &amp; Administration / Permissions).
/// Read from the database rather than <see cref="Permissions.All"/> because the grant editor needs
/// the row ids, not just the keys — the seeder keeps the table in step with the code registry.
/// </summary>
[RequirePermission(Permissions.Administration.ManageRoles)]
public record GetPermissionsQuery : IRequest<IReadOnlyList<PermissionCategoryDto>>;

public class GetPermissionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetPermissionsQuery, IReadOnlyList<PermissionCategoryDto>>
{
    public async Task<IReadOnlyList<PermissionCategoryDto>> Handle(
        GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Permissions.AsNoTracking()
            .OrderBy(p => p.Category).ThenBy(p => p.Key)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Key = p.Key,
                Category = p.Category,
                NameEn = p.Name.En,
                NameAr = p.Name.Ar,
                Description = p.Description,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(p => p.Category)
            .Select(g => new PermissionCategoryDto { Category = g.Key, Permissions = g.ToList() })
            .ToList();
    }
}
