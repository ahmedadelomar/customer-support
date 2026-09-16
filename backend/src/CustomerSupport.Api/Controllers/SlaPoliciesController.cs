using CustomerSupport.Application.Sla.Policies;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>SLA policy administration (SLA and Automation / Response and resolution targets).</summary>
public class SlaPoliciesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SlaPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SlaPolicyDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetSlaPoliciesQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateSlaPolicyCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSlaPolicyCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "The route id and the body id must match." });
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteSlaPolicyCommand(id), ct);
        return NoContent();
    }

    /// <summary>Computes due times for a hypothetical ticket without creating one.</summary>
    [HttpPost("{id:guid}/preview")]
    [ProducesResponseType(typeof(SlaPolicyPreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SlaPolicyPreviewResult>> Preview(
        Guid id, [FromBody] PreviewSlaPolicyRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new PreviewSlaPolicyQuery(id, request.PriorityId, request.ArrivalTime), ct));
}

public record PreviewSlaPolicyRequest(Guid PriorityId, DateTimeOffset ArrivalTime);
