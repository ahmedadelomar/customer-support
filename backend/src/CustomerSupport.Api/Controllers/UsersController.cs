using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Users.Commands;
using CustomerSupport.Application.Users.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Agent account administration (Security &amp; Administration / Users and roles). There is no delete
/// action by design — deactivation replaces it, because tickets and audit rows reference users forever.
/// </summary>
public class UsersController : ApiControllerBase
{
    /// <summary>Paged, filterable list of accounts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> GetList(
        [FromQuery] GetUsersQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>Branches, departments and roles for the form's pickers.</summary>
    [HttpGet("lookups")]
    [ProducesResponseType(typeof(UserLookupsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserLookupsDto>> GetLookups(CancellationToken ct)
        => Ok(await Sender.Send(new GetUserLookupsQuery(), ct));

    /// <summary>One account's full record.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new GetUserByIdQuery(id), ct));

    /// <summary>Creates an account. It starts with a forced password change.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateUserCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Updates an account's profile. The username is immutable.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Restores access to a deactivated account.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetUserActiveCommand(id, true), ct);
        return NoContent();
    }

    /// <summary>Ends access immediately: the account is disabled and its refresh tokens revoked.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetUserActiveCommand(id, false), ct);
        return NoContent();
    }

    /// <summary>Replaces the account's role set. At least one role is required.</summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoles(
        Guid id, [FromBody] UpdateUserRolesRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateUserRolesCommand(id, request.RoleIds), ct);
        return NoContent();
    }

    /// <summary>Sets the caller's own availability. Any signed-in user may call this for themselves.</summary>
    [HttpPut("me/availability")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMyAvailability(
        [FromBody] UpdateAvailabilityRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateMyAvailabilityCommand(request.AvailabilityStatus), ct);
        return NoContent();
    }
}

public record UpdateUserRolesRequest(List<Guid> RoleIds);

public record UpdateAvailabilityRequest(string AvailabilityStatus);
