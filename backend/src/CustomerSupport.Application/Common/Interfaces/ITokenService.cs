using CustomerSupport.Application.Auth.Dtos;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// Issues and rotates the JWT access token and its refresh token. Implemented in Infrastructure,
/// which is where the Identity user type lives.
/// </summary>
public interface ITokenService
{
    /// <summary>Signs in the given credentials and issues a new token pair.</summary>
    /// <exception cref="Common.Exceptions.ForbiddenException">
    /// Thrown for every failure mode — unknown user, wrong password, locked or inactive account —
    /// with the same message, so the endpoint cannot be used to enumerate accounts.
    /// </exception>
    Task<AuthResultDto> LoginAsync(string userName, string password, string? ip, CancellationToken ct = default);

    /// <summary>Rotates a refresh token. Reusing an already-rotated token revokes the whole chain.</summary>
    Task<AuthResultDto> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default);

    /// <summary>Revokes a refresh token. Idempotent — an unknown or already-revoked token is not an error.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
