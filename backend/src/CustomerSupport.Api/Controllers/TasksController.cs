using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Workspace.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Agent tasks (Agent Dashboard / Tasks and reminders).</summary>
public class TasksController : ApiControllerBase
{
    /// <summary>Own tasks by default; <c>assignedToId</c> is honoured only with <c>workspace.tasks.assign</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AgentTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AgentTaskDto>>> GetList(
        [FromQuery] GetTasksQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>Creates a task, optionally with one reminder.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTaskCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { }, id);
    }

    /// <summary>Edits a task's own fields. Reassigning notifies the new assignee.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskCommand command, CancellationToken ct)
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
        await Sender.Send(new DeleteTaskCommand(id), ct);
        return NoContent();
    }

    /// <summary>Completes a task. The task's own assignee or a workspace manager only.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new CompleteTaskCommand(id), ct);
        return NoContent();
    }
}
