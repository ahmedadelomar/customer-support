using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Application.Customers.Notes;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Internal notes on a customer profile, nested under a customer (Customer Management / Notes and attachments).</summary>
/// <remarks>Uses the app-relative route template pattern established by <c>CustomerContactsController</c>.</remarks>
public class CustomerNotesController : ApiControllerBase
{
    /// <summary>Paged notes, pinned first.</summary>
    [HttpGet("/api/customers/{customerId:guid}/notes")]
    [ProducesResponseType(typeof(PagedResult<CustomerNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CustomerNoteDto>>> GetList(
        Guid customerId, [FromQuery] GetCustomerNotesQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query with { CustomerId = customerId }, ct));

    /// <summary>Adds a note.</summary>
    [HttpPost("/api/customers/{customerId:guid}/notes")]
    [ProducesResponseType(typeof(CustomerNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerNoteDto>> Create(
        Guid customerId, [FromBody] CreateCustomerNoteCommand command, CancellationToken ct)
    {
        var result = await Sender.Send(command with { CustomerId = customerId }, ct);
        return CreatedAtAction(nameof(GetList), new { customerId }, result);
    }

    /// <summary>Edits a note's body. Refused unless the caller is the author or holds the manage permission.</summary>
    [HttpPut("/api/customers/{customerId:guid}/notes/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid customerId, Guid id, [FromBody] UpdateCustomerNoteCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { CustomerId = customerId, NoteId = id }, ct);
        return NoContent();
    }

    /// <summary>Soft-deletes a note. Refused unless the caller is the author or holds the manage permission.</summary>
    [HttpDelete("/api/customers/{customerId:guid}/notes/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid customerId, Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteCustomerNoteCommand(customerId, id), ct);
        return NoContent();
    }

    /// <summary>Toggles pin state. Returns the new state.</summary>
    [HttpPost("/api/customers/{customerId:guid}/notes/{id:guid}/pin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> TogglePin(Guid customerId, Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new ToggleNotePinCommand(customerId, id), ct));
}
