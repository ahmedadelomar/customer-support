using CustomerSupport.Application.Workspace.QuickReplies;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Quick replies (Agent Dashboard / Quick replies).</summary>
public class QuickRepliesController : ApiControllerBase
{
    /// <summary>Replies visible to the caller — personal, their teams', and every global one — ordered by usage.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuickReplyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuickReplyDto>>> GetList(
        [FromQuery] GetQuickRepliesQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>Creates a quick reply. Global scope also needs the global permission.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateQuickReplyCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { }, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuickReplyCommand command, CancellationToken ct)
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteQuickReplyCommand(id), ct);
        return NoContent();
    }

    /// <summary>Resolves the reply's body against a ticket and increments its usage count.</summary>
    [HttpPost("{id:guid}/render")]
    [ProducesResponseType(typeof(RenderResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RenderResult>> Render(
        Guid id, [FromBody] RenderQuickReplyCommand command, CancellationToken ct)
        => Ok(await Sender.Send(command with { Id = id }, ct));

    /// <summary>Renders a draft body against a sample ticket — the management screen's live preview, before saving.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(RenderResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RenderResult>> Preview(
        [FromBody] PreviewQuickReplyCommand command, CancellationToken ct)
        => Ok(await Sender.Send(command, ct));
}
