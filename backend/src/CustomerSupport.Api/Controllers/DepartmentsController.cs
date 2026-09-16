using CustomerSupport.Application.Organization.Departments;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Departments (Platform / Departments, teams and queue scoping).</summary>
public class DepartmentsController : ApiControllerBase
{
    /// <summary>Departments for pickers, filters and the admin list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetList(
        [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await Sender.Send(new GetDepartmentsQuery(includeInactive), ct));

    /// <summary>Creates a department.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateDepartmentCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { id }, id);
    }

    /// <summary>Updates a department. The code is immutable.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Deactivates a department. Refused while open tickets or active teams remain.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetDepartmentActiveCommand(id, false), ct);
        return NoContent();
    }

    /// <summary>Reactivates a department.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetDepartmentActiveCommand(id, true), ct);
        return NoContent();
    }
}
