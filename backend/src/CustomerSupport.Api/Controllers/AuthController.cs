using CustomerSupport.Application.Auth.Commands;
using CustomerSupport.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}

public record LoginRequest(string UserName, string Password);

public record RefreshRequest(string RefreshToken);
