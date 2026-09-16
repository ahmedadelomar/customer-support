using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Organization.Departments;

/// <summary>Creates a department (Platform / Departments, teams and queue scoping).</summary>
[RequirePermission(Permissions.Administration.ManageDepartments)]
public record CreateDepartmentCommand : IRequest<Guid>
{
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? ManagerId { get; init; }
    public Guid? BranchId { get; init; }
}

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class CreateDepartmentCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateDepartmentCommand, Guid>
{
    public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        if (await db.Departments.AnyAsync(d => d.Code == code, cancellationToken))
        {
            throw new ConflictException($"A department with the code '{code}' already exists.");
        }

        var department = new Department
        {
            Code = code,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            Description = request.Description,
            ManagerId = request.ManagerId,

            // New records take the caller's active branch unless one is named explicitly.
            BranchId = request.BranchId ?? currentUser.BranchId,
            IsActive = true,
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);

        return department.Id;
    }
}

/// <summary>Updates a department's name, description and manager. The code is immutable.</summary>
[RequirePermission(Permissions.Administration.ManageDepartments)]
public record UpdateDepartmentCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? ManagerId { get; init; }
}

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class UpdateDepartmentCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateDepartmentCommand>
{
    public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.WhereBranchAccessible(currentUser)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.Id);

        department.Name = new LocalizedText(request.NameEn, request.NameAr);
        department.Description = request.Description;
        department.ManagerId = request.ManagerId;

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Activates or deactivates a department. Deactivation is refused while open tickets or active teams
/// remain: the tickets would become invisible to their own queue, and the teams would point at a
/// department nobody can pick.
/// </summary>
[RequirePermission(Permissions.Administration.ManageDepartments)]
public record SetDepartmentActiveCommand(Guid Id, bool IsActive) : IRequest;

public class SetDepartmentActiveCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SetDepartmentActiveCommand>
{
    public async Task Handle(SetDepartmentActiveCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.WhereBranchAccessible(currentUser)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.Id);

        if (!request.IsActive)
        {
            var openTickets = await db.Tickets
                .CountAsync(t => t.DepartmentId == request.Id && !t.Status.IsTerminal, cancellationToken);

            if (openTickets > 0)
            {
                throw new ConflictException(
                    $"Cannot deactivate this department while {openTickets} open ticket(s) remain. Transfer them first.");
            }

            var activeTeams = await db.Teams
                .CountAsync(t => t.DepartmentId == request.Id && t.IsActive, cancellationToken);

            if (activeTeams > 0)
            {
                throw new ConflictException(
                    $"Cannot deactivate this department while {activeTeams} active team(s) belong to it.");
            }
        }

        department.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}
