using CustomerSupport.Application.Automation.AssignmentRules;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Assignment rule administration (SLA and Automation / Automatic assignment).</summary>
public class AssignmentRulesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssignmentRuleDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetAssignmentRulesQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateAssignmentRuleCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reorder([FromBody] ReorderAssignmentRulesCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssignmentRuleCommand command, CancellationToken ct)
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
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteAssignmentRuleCommand(id), ct);
        return NoContent();
    }

    /// <summary>Runs the rule against a real ticket and reports the outcome, writing nothing.</summary>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(CustomerSupport.Application.Common.Interfaces.AssignmentPreview), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerSupport.Application.Common.Interfaces.AssignmentPreview>> Test(
        Guid id, [FromBody] TestAssignmentRuleRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new TestAssignmentRuleQuery(id, request.TicketId), ct));
}

public record TestAssignmentRuleRequest(Guid TicketId);
