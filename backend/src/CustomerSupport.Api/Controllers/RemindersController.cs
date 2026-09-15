using CustomerSupport.Application.Workspace.Reminders;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Reminders (Agent Dashboard / Tasks and reminders).</summary>
public class RemindersController : ApiControllerBase
{
    /// <summary>Fired-but-unacknowledged reminders for the caller — the persistent toast queue.</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingReminderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingReminderDto>>> GetPending(CancellationToken ct)
        => Ok(await Sender.Send(new GetPendingRemindersQuery(), ct));

    /// <summary>Snoozes a reminder by creating a new one; the original stays marked sent.</summary>
    [HttpPost("{id:guid}/snooze")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Snooze(Guid id, [FromBody] SnoozeReminderCommand command, CancellationToken ct)
        => Ok(await Sender.Send(command with { Id = id }, ct));

    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DismissReminderCommand(id), ct);
        return NoContent();
    }
}
