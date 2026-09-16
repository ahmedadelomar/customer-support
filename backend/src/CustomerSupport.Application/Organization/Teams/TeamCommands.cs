using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Organization.Teams;

/// <summary>Creates a team inside a department (Platform / Departments, teams and queue scoping).</summary>
[RequirePermission(Permissions.Administration.ManageTeams)]
public record CreateTeamCommand : IRequest<Guid>
{
    public Guid DepartmentId { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public Guid? LeadUserId { get; init; }
}

public class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class CreateTeamCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateTeamCommand, Guid>
{
    public async Task<Guid> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.WhereBranchAccessible(currentUser)
            .FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.DepartmentId);

        var team = new Team
        {
            DepartmentId = department.Id,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            LeadUserId = request.LeadUserId,

            // Inherits the department's branch so the two can never disagree about scope.
            BranchId = department.BranchId,
            IsActive = true,
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);

        return team.Id;
    }
}

/// <summary>Updates a team's name, lead and active state.</summary>
[RequirePermission(Permissions.Administration.ManageTeams)]
public record UpdateTeamCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public Guid? LeadUserId { get; init; }
    public bool IsActive { get; init; } = true;
}

public class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class UpdateTeamCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateTeamCommand>
{
    public async Task Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await db.Teams.WhereBranchAccessible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), request.Id);

        team.Name = new LocalizedText(request.NameEn, request.NameAr);
        team.LeadUserId = request.LeadUserId;
        team.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>One member as submitted by the team form, in the order the form listed them.</summary>
public record TeamMemberInput(Guid UserId, int MaxConcurrentTickets, bool IsLead);

/// <summary>
/// Replaces a team's membership in one write.
/// </summary>
/// <remarks>
/// <c>RotationOrder</c> is assigned from the submitted order rather than stored per row by the client,
/// so round-robin assignment (CS-502) walks members in exactly the order an administrator arranged
/// them, deterministically across restarts.
/// </remarks>
[RequirePermission(Permissions.Administration.ManageTeams)]
public record UpdateTeamMembersCommand(Guid TeamId, List<TeamMemberInput> Members) : IRequest;

public class UpdateTeamMembersCommandValidator : AbstractValidator<UpdateTeamMembersCommand>
{
    public UpdateTeamMembersCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleForEach(x => x.Members).ChildRules(member =>
        {
            member.RuleFor(m => m.UserId).NotEmpty();
            member.RuleFor(m => m.MaxConcurrentTickets).GreaterThanOrEqualTo(0);
        });

        RuleFor(x => x.Members)
            .Must(members => members.Select(m => m.UserId).Distinct().Count() == members.Count)
            .WithMessage("The same agent cannot appear twice on one team.");
    }
}

public class UpdateTeamMembersCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<UpdateTeamMembersCommand>
{
    public async Task Handle(UpdateTeamMembersCommand request, CancellationToken cancellationToken)
    {
        var team = await db.Teams.WhereBranchAccessible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), request.TeamId);

        var existing = await db.TeamMembers
            .Where(m => m.TeamId == team.Id)
            .ToListAsync(cancellationToken);

        db.TeamMembers.RemoveRange(existing);

        for (var index = 0; index < request.Members.Count; index++)
        {
            var member = request.Members[index];

            db.TeamMembers.Add(new TeamMember
            {
                TeamId = team.Id,
                UserId = member.UserId,
                IsLead = member.IsLead,
                MaxConcurrentTickets = member.MaxConcurrentTickets,
                RotationOrder = index,
                IsActive = true,

                // Preserved across a membership rewrite so "joined" reflects the team, not the edit.
                JoinedAt = existing.FirstOrDefault(e => e.UserId == member.UserId)?.JoinedAt ?? clock.UtcNow,
            });
        }

        team.LeadUserId = request.Members.FirstOrDefault(m => m.IsLead)?.UserId ?? team.LeadUserId;

        // The cursor indexes into the member list; a changed roster makes the old position meaningless.
        team.RoundRobinCursor = 0;

        await db.SaveChangesAsync(cancellationToken);
    }
}
