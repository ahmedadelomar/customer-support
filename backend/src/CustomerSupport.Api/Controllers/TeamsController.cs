using CustomerSupport.Application.Organization.Teams;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Teams and their membership (Platform / Departments, teams and queue scoping). Membership carries
/// the rotation order and per-member capacity that automatic assignment depends on.
/// </summary>
public class TeamsController : ApiControllerBase
{
    /// <summary>Teams with their members, optionally filtered to one department.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetList(
        [FromQuery] Guid? departmentId, [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await Sender.Send(new GetTeamsQuery(departmentId, includeInactive), ct));

    /// <summary>Creates a team inside a department.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTeamCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { id }, id);
    }

    /// <summary>Updates a team's name, lead and active state.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Replaces the membership set, taking rotation order from the submitted order.</summary>
    [HttpPut("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMembers(
        Guid id, [FromBody] UpdateTeamMembersRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateTeamMembersCommand(id, request.Members), ct);
        return NoContent();
    }
}

public record UpdateTeamMembersRequest(List<TeamMemberInput> Members);
