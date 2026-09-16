using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;

namespace CustomerSupport.Application.Users.Commands;

/// <summary>
/// Creates an agent account (Security &amp; Administration / Users and roles). The account is always
/// created with <c>MustChangePassword</c> set: an administrator-chosen password is a shared secret
/// until the owner replaces it.
/// </summary>
[RequirePermission(Permissions.Administration.ManageUsers)]
public record CreateUserCommand : IRequest<Guid>
{
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string Password { get; init; } = string.Empty;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public Guid? BranchId { get; init; }
    public List<Guid> AccessibleBranchIds { get; init; } = [];
    public Guid? DepartmentId { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public string? TimeZoneId { get; init; }
    public int MaxConcurrentTickets { get; init; }
    public List<Guid> RoleIds { get; init; } = [];
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).Must(v => v is "ar" or "en")
            .WithMessage("Preferred language must be 'ar' or 'en'.");
        RuleFor(x => x.MaxConcurrentTickets).GreaterThanOrEqualTo(0);

        // Mirrors the server-side rule the role editor also enforces: an account with no role can
        // sign in but do nothing, which reads as a broken account rather than a restricted one.
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("A user must hold at least one role.");
    }
}

public class CreateUserCommandHandler(IUserAdminService users) : IRequestHandler<CreateUserCommand, Guid>
{
    public Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken) =>
        users.CreateAsync(
            new CreateUserRequest
            {
                UserName = request.UserName,
                Email = request.Email,
                Password = request.Password,
                DisplayName = new LocalizedText(request.DisplayNameEn, request.DisplayNameAr),
                JobTitle = request.JobTitle,
                BranchId = request.BranchId,
                AccessibleBranchIds = request.AccessibleBranchIds,
                DepartmentId = request.DepartmentId,
                PreferredLanguage = request.PreferredLanguage,
                TimeZoneId = request.TimeZoneId,
                MaxConcurrentTickets = request.MaxConcurrentTickets,
                RoleIds = request.RoleIds,
            },
            cancellationToken);
}
