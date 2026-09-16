using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;

namespace CustomerSupport.Application.Users.Commands;

/// <summary>
/// Updates an account's profile. The username is deliberately immutable — it appears in audit rows
/// and ticket-event actors, and renaming it retroactively rewrites who did what.
/// </summary>
[RequirePermission(Permissions.Administration.ManageUsers)]
public record UpdateUserCommand : IRequest
{
    public Guid Id { get; init; }
    public string? Email { get; init; }
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public Guid? BranchId { get; init; }
    public List<Guid> AccessibleBranchIds { get; init; } = [];
    public Guid? DepartmentId { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public string? TimeZoneId { get; init; }
    public int MaxConcurrentTickets { get; init; }
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).Must(v => v is "ar" or "en")
            .WithMessage("Preferred language must be 'ar' or 'en'.");
        RuleFor(x => x.MaxConcurrentTickets).GreaterThanOrEqualTo(0);
    }
}

public class UpdateUserCommandHandler(IUserAdminService users) : IRequestHandler<UpdateUserCommand>
{
    public Task Handle(UpdateUserCommand request, CancellationToken cancellationToken) =>
        users.UpdateAsync(
            request.Id,
            new UpdateUserRequest
            {
                Email = request.Email,
                DisplayName = new LocalizedText(request.DisplayNameEn, request.DisplayNameAr),
                JobTitle = request.JobTitle,
                BranchId = request.BranchId,
                AccessibleBranchIds = request.AccessibleBranchIds,
                DepartmentId = request.DepartmentId,
                PreferredLanguage = request.PreferredLanguage,
                TimeZoneId = request.TimeZoneId,
                MaxConcurrentTickets = request.MaxConcurrentTickets,
            },
            cancellationToken);
}

/// <summary>
/// Activates or deactivates an account. There is deliberately no delete: tickets, ticket events and
/// audit rows reference the user id forever, so removing the row would orphan all of them.
/// </summary>
[RequirePermission(Permissions.Administration.ManageUsers)]
public record SetUserActiveCommand(Guid Id, bool IsActive) : IRequest;

public class SetUserActiveCommandHandler(IUserAdminService users, ICurrentUser currentUser)
    : IRequestHandler<SetUserActiveCommand>
{
    public Task Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsActive && request.Id == currentUser.UserId)
        {
            throw new Common.Exceptions.ConflictException("You cannot deactivate your own account.");
        }

        return users.SetActiveAsync(request.Id, request.IsActive, cancellationToken);
    }
}

/// <summary>Replaces the account's role set. Refused when it would leave the account with none.</summary>
[RequirePermission(Permissions.Administration.ManageRoles)]
public record UpdateUserRolesCommand(Guid Id, List<Guid> RoleIds) : IRequest;

public class UpdateUserRolesCommandValidator : AbstractValidator<UpdateUserRolesCommand>
{
    public UpdateUserRolesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("A user must hold at least one role.");
    }
}

public class UpdateUserRolesCommandHandler(IUserAdminService users) : IRequestHandler<UpdateUserRolesCommand>
{
    public Task Handle(UpdateUserRolesCommand request, CancellationToken cancellationToken) =>
        users.ReplaceRolesAsync(request.Id, request.RoleIds, cancellationToken);
}

/// <summary>
/// Sets the caller's own availability (Available / Busy / Away / Offline), which the assignment
/// capacity check reads. No permission attribute: everyone may set their own status, and the handler
/// only ever writes the caller's own row.
/// </summary>
public record UpdateMyAvailabilityCommand(string AvailabilityStatus) : IRequest;

public class UpdateMyAvailabilityCommandValidator : AbstractValidator<UpdateMyAvailabilityCommand>
{
    private static readonly string[] Allowed = ["Available", "Busy", "Away", "Offline"];

    public UpdateMyAvailabilityCommandValidator()
    {
        RuleFor(x => x.AvailabilityStatus).Must(v => Allowed.Contains(v))
            .WithMessage($"Availability must be one of: {string.Join(", ", Allowed)}.");
    }
}

public class UpdateMyAvailabilityCommandHandler(IUserAdminService users, ICurrentUser currentUser)
    : IRequestHandler<UpdateMyAvailabilityCommand>
{
    public Task Handle(UpdateMyAvailabilityCommand request, CancellationToken cancellationToken) =>
        users.SetAvailabilityAsync(currentUser.UserId!.Value, request.AvailabilityStatus, cancellationToken);
}
