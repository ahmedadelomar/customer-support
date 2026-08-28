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
