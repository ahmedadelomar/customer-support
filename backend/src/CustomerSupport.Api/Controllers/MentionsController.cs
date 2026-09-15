using CustomerSupport.Application.Workspace.Collaboration;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>The caller's own @mentions inbox (Agent Dashboard / Team collaboration).</summary>
public class MentionsController : ApiControllerBase
{
    /// <summary>The caller's mentions, unread first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MentionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MentionDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetMentionsQuery(), ct));

    /// <summary>Marks one mention read. Only the mentioned user may call this.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await Sender.Send(new MarkMentionReadCommand(id), ct);
        return NoContent();
    }
}
