using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Customers.Commands;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Application.Customers.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Customer profiles (Customer Management). This controller is the reference shape for every other
/// feature controller in the solution: bind, send, return.
/// </summary>
public class CustomersController : ApiControllerBase
{
    /// <summary>Returns a paged, filterable list of customers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerListItemDto>>> GetList(
        [FromQuery] GetCustomersQuery query,
        CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>Returns one customer profile with its contacts and header counts.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new GetCustomerByIdQuery(id), ct));

    /// <summary>Creates a customer together with its primary email and phone contacts.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateCustomerCommand command,
        CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Updates profile fields. Contacts are managed through their own endpoints.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerCommand command,
        CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "The route id and the body id must match." });
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Soft-deletes a customer. Refused while the customer still has open tickets.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteCustomerCommand(id), ct);
        return NoContent();
    }
}
