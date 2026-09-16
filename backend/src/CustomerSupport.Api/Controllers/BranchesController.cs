using CustomerSupport.Application.Organization.Branches;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Branches (Platform / Branch scoping and the branch switcher).</summary>
public class BranchesController : ApiControllerBase
{
    /// <summary>Branches for the switcher and the admin list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetList(
        [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await Sender.Send(new GetBranchesQuery(includeInactive), ct));

    /// <summary>Creates a branch.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateBranchCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { id }, id);
    }

    /// <summary>Updates a branch. The code is immutable.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Deactivates a branch. Refused while active users or open tickets remain.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetBranchActiveCommand(id, false), ct);
        return NoContent();
    }

    /// <summary>Reactivates a branch.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetBranchActiveCommand(id, true), ct);
        return NoContent();
    }
}
