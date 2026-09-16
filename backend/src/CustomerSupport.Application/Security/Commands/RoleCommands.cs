using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Security.Commands;

/// <summary>Creates a custom role. Permissions are granted separately, through the matrix editor.</summary>
[RequirePermission(Permissions.Administration.ManageRoles)]
public record CreateRoleCommand : IRequest<Guid>
{
    public string Name { get; init; } = string.Empty;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128)
            .Matches("^[A-Za-z0-9_]+$")
            .WithMessage("The role name may contain only letters, digits and underscores.");
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
    }
}

public class CreateRoleCommandHandler(IRoleAdminService roles) : IRequestHandler<CreateRoleCommand, Guid>
{
    public Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken) =>
        roles.CreateAsync(
            request.Name,
            new LocalizedText(request.DisplayNameEn, request.DisplayNameAr),
            request.Description,
            cancellationToken);
}

/// <summary>Renames a custom role. System roles are refused — their names are referenced in code.</summary>
[RequirePermission(Permissions.Administration.ManageRoles)]
public record UpdateRoleCommand : IRequest
{
    public Guid Id { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
    }
}

public class UpdateRoleCommandHandler(IRoleAdminService roles) : IRequestHandler<UpdateRoleCommand>
{
    public Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken) =>
        roles.RenameAsync(
            request.Id,
            new LocalizedText(request.DisplayNameEn, request.DisplayNameAr),
            request.Description,
            cancellationToken);
}

/// <summary>Deletes a custom role. Refused for system roles and for roles a user still holds.</summary>
[RequirePermission(Permissions.Administration.ManageRoles)]
public record DeleteRoleCommand(Guid Id) : IRequest;

public class DeleteRoleCommandHandler(IRoleAdminService roles) : IRequestHandler<DeleteRoleCommand>
{
    public Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken) =>
        roles.DeleteAsync(request.Id, cancellationToken);
}

/// <summary>
/// Replaces a role's permission grants (Security &amp; Administration / Permissions).
/// Applies the delta rather than a wholesale replace, so the audit trail records what actually
/// changed instead of "every grant rewritten".
/// </summary>
[RequirePermission(Permissions.Administration.ManageRoles)]
public record UpdateRolePermissionsCommand(Guid RoleId, List<Guid> PermissionIds) : IRequest;

public class UpdateRolePermissionsCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IUserAdminService users,
    IAuditRecorder audit,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateRolePermissionsCommand>
{
    public async Task Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .ToListAsync(cancellationToken);

        var currentIds = existing.Select(rp => rp.PermissionId).ToHashSet();
        var targetIds = request.PermissionIds.ToHashSet();

        var toAdd = targetIds.Except(currentIds).ToList();
        var toRemove = currentIds.Except(targetIds).ToList();

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            return;
        }

        await GuardSelfLockoutAsync(request.RoleId, toRemove, cancellationToken);

        foreach (var permissionId in toAdd)
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = request.RoleId,
                PermissionId = permissionId,
                GrantedAt = clock.UtcNow,
                GrantedById = currentUser.UserId,
            });
        }

        if (toRemove.Count > 0)
        {
            db.RolePermissions.RemoveRange(existing.Where(rp => toRemove.Contains(rp.PermissionId)));
        }

        await db.SaveChangesAsync(cancellationToken);

        // The interceptor only audits entities on its opt-in list; a grant change is significant
        // enough to record explicitly, with the keys rather than the row ids.
        var keys = await db.Permissions
            .Where(p => toAdd.Contains(p.Id) || toRemove.Contains(p.Id))
            .Select(p => new { p.Id, p.Key })
            .ToDictionaryAsync(p => p.Id, p => p.Key, cancellationToken);

        await audit.RecordAsync(
            AuditAction.PermissionChanged,
            "Role",
            request.RoleId.ToString(),
            new
            {
                Granted = toAdd.Select(id => keys.GetValueOrDefault(id)).ToList(),
                Revoked = toRemove.Select(id => keys.GetValueOrDefault(id)).ToList(),
            },
            ct: cancellationToken);
    }

    /// <summary>
    /// Refuses a change that would strip role management from the caller's last role holding it —
    /// otherwise an administrator can lock the entire organisation out of role management with one save.
    /// </summary>
    private async Task GuardSelfLockoutAsync(Guid roleId, List<Guid> removing, CancellationToken ct)
    {
        if (removing.Count == 0 || currentUser.UserId is not { } userId)
        {
            return;
        }

        var manageRolesId = await db.Permissions
            .Where(p => p.Key == Permissions.Administration.ManageRoles)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (manageRolesId == Guid.Empty || !removing.Contains(manageRolesId))
        {
            return;
        }

        var callerRoleIds = await users.GetRoleIdsForUserAsync(userId, ct);
        if (!callerRoleIds.Contains(roleId))
        {
            return;
        }

        var stillGrantedElsewhere = await db.RolePermissions.AnyAsync(
            rp => rp.PermissionId == manageRolesId &&
                  rp.RoleId != roleId &&
                  callerRoleIds.Contains(rp.RoleId),
            ct);

        if (!stillGrantedElsewhere)
        {
            throw new ConflictException(
                "This change would remove your own ability to manage roles. Grant it to another role first.");
        }
    }
}
