using CustomerSupport.Application.Tickets.Statuses;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Status workflow administration (Ticket Management / Status workflow and escalation).</summary>
public class TicketStatusesController : ApiControllerBase
{
    /// <summary>The full workflow, active and inactive, ordered by display order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TicketStatusAdminDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketStatusAdminDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetTicketStatusesAdminQuery(), ct));

    /// <summary>Creates a status, appended to the end of the workflow order.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTicketStatusCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    /// <summary>Reorders the whole workflow — dragging a row in the admin list.</summary>
    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder([FromBody] ReorderTicketStatusesCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Edits names, kind, colour and flags. Changing kind while tickets use this status needs <c>force: true</c>.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketStatusCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "The route id and the body id must match." });
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Deletes a status. Refused while referenced by a ticket, the default, or the last Closed-kind status.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteTicketStatusCommand(id), ct);
        return NoContent();
    }
}
