using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Automation.EscalationRules;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Escalation rule administration (SLA and Automation / Escalation rules).</summary>
public class EscalationRulesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EscalationRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EscalationRuleDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetEscalationRulesQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateEscalationRuleCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reorder([FromBody] ReorderEscalationRulesCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEscalationRuleCommand command, CancellationToken ct)
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
        await Sender.Send(new DeleteEscalationRuleCommand(id), ct);
        return NoContent();
    }

    /// <summary>Dry-run: the tickets this rule would fire on right now. Fires nothing.</summary>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(IReadOnlyList<EscalationTestMatch>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EscalationTestMatch>>> Test(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new TestEscalationRuleQuery(id), ct));
}
