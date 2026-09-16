using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Users.Queries;

/// <summary>A branch or department as offered by the user form's pickers.</summary>
public record ScopeLookupDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
}

/// <summary>Everything the user form's pickers need in one request.</summary>
public record UserLookupsDto
{
    public IReadOnlyList<ScopeLookupDto> Branches { get; init; } = [];
    public IReadOnlyList<ScopeLookupDto> Departments { get; init; } = [];
    public IReadOnlyList<RoleSummaryDto> Roles { get; init; } = [];
}

/// <summary>
/// Branches, departments and roles for the user form. Branches are not exposed by any other endpoint
/// yet, and reaching into the tickets feature's lookups for departments would couple administration
/// to a feature it has nothing to do with — so this screen has its own.
/// </summary>
[RequirePermission(Permissions.Administration.ViewUsers)]
public record GetUserLookupsQuery : IRequest<UserLookupsDto>;

public class GetUserLookupsQueryHandler(IAppDbContext db, IRoleAdminService roles)
    : IRequestHandler<GetUserLookupsQuery, UserLookupsDto>
{
    public async Task<UserLookupsDto> Handle(GetUserLookupsQuery request, CancellationToken cancellationToken) =>
        new()
        {
            Branches = await db.Branches.AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name.En)
                .Select(b => new ScopeLookupDto { Id = b.Id, NameEn = b.Name.En, NameAr = b.Name.Ar })
                .ToListAsync(cancellationToken),

            Departments = await db.Departments.AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name.En)
                .Select(d => new ScopeLookupDto { Id = d.Id, NameEn = d.Name.En, NameAr = d.Name.Ar })
                .ToListAsync(cancellationToken),

            Roles = await roles.ListAsync(cancellationToken),
        };
}
