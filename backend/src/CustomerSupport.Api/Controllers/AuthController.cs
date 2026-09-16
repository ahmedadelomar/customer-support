using CustomerSupport.Api.Infrastructure;
using CustomerSupport.Application.Auth.Commands;
using CustomerSupport.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Sign-in, token refresh and sign-out. Every action here is anonymous by design; the tokens it
/// issues are what every other controller authorises against.
/// </summary>
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    /// <summary>Signs in and returns an access token, a refresh token and the user profile.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResultDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
        => Ok(await Sender.Send(new LoginCommand(request.UserName, request.Password, ClientIp), ct));

    /// <summary>Exchanges a refresh token for a new pair. The presented token is rotated out.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResultDto>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
        => Ok(await Sender.Send(new RefreshTokenCommand(request.RefreshToken, ClientIp), ct));

    /// <summary>Revokes the presented refresh token. Idempotent.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await Sender.Send(new LogoutCommand(request.RefreshToken), ct);
        return NoContent();
    }

    /// <summary>
    /// Changes the caller's own password and returns a fresh session. Reachable while
    /// <c>MustChangePassword</c> is set — it is the way out of that state.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResultDto>> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct));

    /// <summary>
    /// Switches the caller's active branch and returns a fresh session scoped to it. Refused for a
    /// branch outside the caller's accessible set.
    /// </summary>
    [HttpPost("switch-branch")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResultDto>> SwitchBranch(
        [FromBody] SwitchBranchRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new SwitchBranchCommand(request.BranchId, ClientIp), ct));

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}

public record LoginRequest(string UserName, string Password);

public record RefreshRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record SwitchBranchRequest(Guid BranchId);
