using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Organization.Teams;

/// <summary>One member of a team, with the capacity and rotation order assignment depends on.</summary>
public record TeamMemberDto
{
    public Guid UserId { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public bool IsLead { get; init; }
    public int MaxConcurrentTickets { get; init; }
    public int RotationOrder { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>A team with its membership.</summary>
public record TeamDto
{
    public Guid Id { get; init; }
    public Guid DepartmentId { get; init; }
    public string DepartmentNameEn { get; init; } = string.Empty;
    public string DepartmentNameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public Guid? LeadUserId { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<TeamMemberDto> Members { get; init; } = [];
}

/// <summary>
/// Teams, optionally filtered to one department (Platform / Departments, teams and queue scoping).
/// Carries the rotation order and per-member capacity that automatic assignment (CS-502) reads.
/// </summary>
[RequirePermission(Permissions.Administration.ManageTeams)]
public record GetTeamsQuery(Guid? DepartmentId = null, bool IncludeInactive = false)
    : IRequest<IReadOnlyList<TeamDto>>;

public class GetTeamsQueryHandler(
    IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async Task<IReadOnlyList<TeamDto>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Teams.AsNoTracking().WhereBranchAccessible(currentUser);

        if (request.DepartmentId is { } departmentId)
        {
            query = query.Where(t => t.DepartmentId == departmentId);
        }

        if (!request.IncludeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var teams = await query
            .OrderBy(t => t.Name.En)
            .Select(t => new
            {
                t.Id,
                t.DepartmentId,
                DepartmentName = t.Department.Name,
                t.Name,
                t.LeadUserId,
                t.IsActive,
            })
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(t => t.Id).ToList();

        var members = await db.TeamMembers.AsNoTracking()
            .Where(m => teamIds.Contains(m.TeamId))
            .OrderBy(m => m.RotationOrder)
            .Select(m => new
            {
                m.TeamId,
                m.UserId,
                m.IsLead,
                m.MaxConcurrentTickets,
                m.RotationOrder,
                m.IsActive,
            })
            .ToListAsync(cancellationToken);

        var names = await userNames.ResolveAsync(members.Select(m => m.UserId).Distinct(), cancellationToken);

        return teams.Select(t => new TeamDto
        {
            Id = t.Id,
            DepartmentId = t.DepartmentId,
            DepartmentNameEn = t.DepartmentName.En,
            DepartmentNameAr = t.DepartmentName.Ar,
            NameEn = t.Name.En,
            NameAr = t.Name.Ar,
            LeadUserId = t.LeadUserId,
            IsActive = t.IsActive,
            Members = members
                .Where(m => m.TeamId == t.Id)
                .Select(m => new TeamMemberDto
                {
                    UserId = m.UserId,
                    NameEn = names.GetValueOrDefault(m.UserId)?.En ?? "",
                    NameAr = names.GetValueOrDefault(m.UserId)?.Ar ?? "",
                    IsLead = m.IsLead,
                    MaxConcurrentTickets = m.MaxConcurrentTickets,
                    RotationOrder = m.RotationOrder,
                    IsActive = m.IsActive,
                })
                .ToList(),
        }).ToList();
    }
}
