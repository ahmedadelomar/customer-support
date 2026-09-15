using CustomerSupport.Application.Tickets.Priorities;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Priority scale administration (Ticket Management / Categories and priorities).</summary>
public class TicketPrioritiesController : ApiControllerBase
{
    /// <summary>The full scale, active and inactive, ordered by level.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TicketPriorityAdminDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketPriorityAdminDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetTicketPrioritiesAdminQuery(), ct));

    /// <summary>Creates a priority, appended to the end of the scale.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTicketPriorityCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    /// <summary>Reorders the whole scale — dragging a row in the admin list.</summary>
    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder([FromBody] ReorderTicketPrioritiesCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Edits names, colour, icon, default flag and active state.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketPriorityCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "The route id and the body id must match." });
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Deletes a priority. Refused while an SLA target, a ticket, or the default flag references it.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteTicketPriorityCommand(id), ct);
        return NoContent();
    }
}
