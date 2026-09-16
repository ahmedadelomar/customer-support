using CustomerSupport.Application.Security.Commands;
using CustomerSupport.Application.Security.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Roles and their permission grants (Security &amp; Administration / Permissions). System roles are
/// protected from rename and deletion, but their grants remain editable — an organisation may
/// legitimately want a narrower Agent role.
/// </summary>
public class RolesController : ApiControllerBase
{
    /// <summary>Every role with its granted permissions and how many users hold it.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetRolesQuery(), ct));

    /// <summary>Creates a custom role. Permissions are granted separately.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateRoleCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { id }, id);
    }

    /// <summary>Renames a custom role. Refused for system roles.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Replaces the role's permission grants. Applies the delta and audits it.</summary>
    [HttpPut("{id:guid}/permissions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id, [FromBody] UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateRolePermissionsCommand(id, request.PermissionIds), ct);
        return NoContent();
    }

    /// <summary>Deletes a custom role. Refused for system roles and for roles still held by a user.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteRoleCommand(id), ct);
        return NoContent();
    }
}

public record UpdateRolePermissionsRequest(List<Guid> PermissionIds);
