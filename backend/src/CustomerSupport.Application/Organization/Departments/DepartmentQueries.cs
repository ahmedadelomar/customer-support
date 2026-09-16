using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Organization.Departments;

/// <summary>A department as shown in pickers and the admin list.</summary>
public record DepartmentDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? ManagerId { get; init; }
    public string? ManagerNameEn { get; init; }
    public string? ManagerNameAr { get; init; }
    public Guid? BranchId { get; init; }
    public bool IsActive { get; init; }
    public int TeamCount { get; init; }
    public int OpenTicketCount { get; init; }
}

/// <summary>
/// Departments for the admin screen and for every department picker
/// (Platform / Departments, teams and queue scoping).
/// </summary>
/// <remarks>
/// Gated on <c>tickets.view</c> rather than <c>admin.departments.manage</c>: the ticket form, the
/// transfer dialog and the list filter all need this, and an agent who can see tickets can already
/// see which departments exist from the tickets themselves.
/// </remarks>
[RequirePermission(Permissions.Tickets.View)]
public record GetDepartmentsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<DepartmentDto>>;

public class GetDepartmentsQueryHandler(
    IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(
        GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Departments.AsNoTracking().WhereBranchAccessible(currentUser);

        if (!request.IncludeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        var rows = await query
            .OrderBy(d => d.Name.En)
            .Select(d => new
            {
                d.Id,
                d.Code,
                d.Name,
                d.Description,
                d.ManagerId,
                d.BranchId,
                d.IsActive,
                TeamCount = db.Teams.Count(t => t.DepartmentId == d.Id && t.IsActive),
                OpenTicketCount = db.Tickets.Count(t => t.DepartmentId == d.Id && !t.Status.IsTerminal),
            })
            .ToListAsync(cancellationToken);

        var managers = await userNames.ResolveAsync(
            rows.Where(r => r.ManagerId is not null).Select(r => r.ManagerId!.Value),
            cancellationToken);

        return rows.Select(r =>
        {
            var manager = r.ManagerId is { } id ? managers.GetValueOrDefault(id) : null;

            return new DepartmentDto
            {
                Id = r.Id,
                Code = r.Code,
                NameEn = r.Name.En,
                NameAr = r.Name.Ar,
                Description = r.Description,
                ManagerId = r.ManagerId,
                ManagerNameEn = manager?.En,
                ManagerNameAr = manager?.Ar,
                BranchId = r.BranchId,
                IsActive = r.IsActive,
                TeamCount = r.TeamCount,
                OpenTicketCount = r.OpenTicketCount,
            };
        }).ToList();
    }
}
