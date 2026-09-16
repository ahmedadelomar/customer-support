using CustomerSupport.Application.Auth.Dtos;
using CustomerSupport.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace CustomerSupport.Application.Auth.Commands;

/// <summary>
/// Signs in with username and password.
/// Deliberately carries no <c>[RequirePermission]</c> — this is the anonymous entry point.
/// </summary>
public record LoginCommand(string UserName, string Password, string? IpAddress = null)
    : IRequest<AuthResultDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public class LoginCommandHandler(ITokenService tokens) : IRequestHandler<LoginCommand, AuthResultDto>
{
    public Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        tokens.LoginAsync(request.UserName, request.Password, request.IpAddress, cancellationToken);
}

/// <summary>Exchanges a refresh token for a new pair, rotating the old one.</summary>
public record RefreshTokenCommand(string RefreshToken, string? IpAddress = null)
    : IRequest<AuthResultDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler(ITokenService tokens)
    : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    public Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) =>
        tokens.RefreshAsync(request.RefreshToken, request.IpAddress, cancellationToken);
}

/// <summary>Revokes the presented refresh token.</summary>
public record LogoutCommand(string RefreshToken) : IRequest;

public class LogoutCommandHandler(ITokenService tokens) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        tokens.LogoutAsync(request.RefreshToken, cancellationToken);
}

/// <summary>
/// Changes the caller's own password and clears <c>MustChangePassword</c>. Returns a fresh session:
/// the old access token still carries the <c>must_change_password</c> claim, so without new tokens the
/// caller would remain locked out of every other endpoint by the middleware that enforces it.
/// No <c>[RequirePermission]</c> — this is one of the two endpoints that must stay reachable while
/// that flag is set.
/// </summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<AuthResultDto>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .WithMessage("The new password must be at least 8 characters.");
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password must differ from the current one.");
    }
}

public class ChangePasswordCommandHandler(
    IUserAdminService users, ITokenService tokens, ICurrentUser currentUser)
    : IRequestHandler<ChangePasswordCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new Common.Exceptions.ForbiddenException("Not signed in.");

        await users.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);

        return await tokens.LoginAsync(
            currentUser.UserName ?? string.Empty, request.NewPassword, null, cancellationToken);
    }
}
