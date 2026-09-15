using CustomerSupport.Application.Tickets.Categories;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Category tree administration (Ticket Management / Categories and priorities).</summary>
public class TicketCategoriesController : ApiControllerBase
{
    /// <summary>The full tree, active and inactive, ordered by path.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TicketCategoryAdminDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryAdminDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetTicketCategoriesAdminQuery(), ct));

    /// <summary>Creates a category node under an optional parent.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTicketCategoryCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    /// <summary>Edits names, code, description, portal visibility and defaults. Reparenting is a separate action.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketCategoryCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "The route id and the body id must match." });
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Reparents a node, rewriting its whole subtree's path and depth. Refused for a self-descendant move.</summary>
    [HttpPost("{id:guid}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveCategoryRequest request, CancellationToken ct)
    {
        await Sender.Send(new MoveTicketCategoryCommand { Id = id, NewParentId = request.NewParentId }, ct);
        return NoContent();
    }

    /// <summary>Deactivates a category. It disappears from the create picker but still renders on existing tickets.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetTicketCategoryActiveCommand(id, false), ct);
        return NoContent();
    }

    /// <summary>Reactivates a previously deactivated category.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetTicketCategoryActiveCommand(id, true), ct);
        return NoContent();
    }
}

public record MoveCategoryRequest(Guid? NewParentId);
