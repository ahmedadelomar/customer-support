using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Organization.Branches;

/// <summary>A branch, as shown in the admin list and the branch switcher.</summary>
public record BranchDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? TimeZoneId { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public int UserCount { get; init; }
    public int OpenTicketCount { get; init; }
}

/// <summary>
/// Branches (Platform / Branch scoping). Readable by any signed-in user: the switcher needs the names
/// of the branches they may work in, and a branch name is not sensitive.
/// </summary>
public record GetBranchesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<BranchDto>>;

public class GetBranchesQueryHandler(IAppDbContext db, IUserAdminService users)
    : IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public async Task<IReadOnlyList<BranchDto>> Handle(
        GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Branches.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        var rows = await query
            .OrderBy(b => b.Name.En)
            .Select(b => new
            {
                b.Id,
                b.Code,
                b.Name,
                b.TimeZoneId,
                b.Address,
                b.PhoneNumber,
                b.IsActive,
                OpenTicketCount = db.Tickets.Count(t => t.BranchId == b.Id && !t.Status.IsTerminal),
            })
            .ToListAsync(cancellationToken);

        var userCounts = await users.CountByBranchAsync(cancellationToken);

        return rows.Select(b => new BranchDto
        {
            Id = b.Id,
            Code = b.Code,
            NameEn = b.Name.En,
            NameAr = b.Name.Ar,
            TimeZoneId = b.TimeZoneId,
            Address = b.Address,
            PhoneNumber = b.PhoneNumber,
            IsActive = b.IsActive,
            UserCount = userCounts.GetValueOrDefault(b.Id),
            OpenTicketCount = b.OpenTicketCount,
        }).ToList();
    }
}

/// <summary>Creates a branch.</summary>
[RequirePermission(Permissions.Administration.ManageBranches)]
public record CreateBranchCommand : IRequest<Guid>
{
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? TimeZoneId { get; init; } = "Asia/Riyadh";
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
}

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class CreateBranchCommandHandler(IAppDbContext db) : IRequestHandler<CreateBranchCommand, Guid>
{
    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        if (await db.Branches.AnyAsync(b => b.Code == code, cancellationToken))
        {
            throw new ConflictException($"A branch with the code '{code}' already exists.");
        }

        var branch = new Branch
        {
            Code = code,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            TimeZoneId = request.TimeZoneId,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}

/// <summary>Updates a branch. The code is immutable.</summary>
[RequirePermission(Permissions.Administration.ManageBranches)]
public record UpdateBranchCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? TimeZoneId { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
}

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class UpdateBranchCommandHandler(IAppDbContext db) : IRequestHandler<UpdateBranchCommand>
{
    public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        branch.Name = new LocalizedText(request.NameEn, request.NameAr);
        branch.TimeZoneId = request.TimeZoneId;
        branch.Address = request.Address;
        branch.PhoneNumber = request.PhoneNumber;

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Activates or deactivates a branch. Deactivation is refused while active users or open tickets
/// remain — otherwise those users sign in to a branch that no longer exists in any picker.
/// </summary>
[RequirePermission(Permissions.Administration.ManageBranches)]
public record SetBranchActiveCommand(Guid Id, bool IsActive) : IRequest;

public class SetBranchActiveCommandHandler(IAppDbContext db, IUserAdminService users)
    : IRequestHandler<SetBranchActiveCommand>
{
    public async Task Handle(SetBranchActiveCommand request, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        if (!request.IsActive)
        {
            var activeUsers = (await users.CountByBranchAsync(cancellationToken)).GetValueOrDefault(request.Id);
            if (activeUsers > 0)
            {
                throw new ConflictException(
                    $"Cannot deactivate this branch while {activeUsers} active user(s) belong to it.");
            }

            var openTickets = await db.Tickets
                .CountAsync(t => t.BranchId == request.Id && !t.Status.IsTerminal, cancellationToken);

            if (openTickets > 0)
            {
                throw new ConflictException(
                    $"Cannot deactivate this branch while {openTickets} open ticket(s) remain.");
            }
        }

        branch.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}
