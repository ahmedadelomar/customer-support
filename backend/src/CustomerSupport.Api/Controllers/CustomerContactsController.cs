using CustomerSupport.Application.Customers.Contacts;
using CustomerSupport.Application.Customers.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Contact details management, nested under a customer (Customer Management / Contact details).
/// </summary>
/// <remarks>
/// Every action uses an app-relative route template (starting with <c>/</c>) rather than relying on
/// the <c>ApiControllerBase</c> class-level <c>[Route("api/[controller]")]</c> — an absolute action
/// template is not combined with the controller template, so this is the standard way to nest a
/// resource under a different controller's path without route-token gymnastics or ambiguous
/// matches. Follow this pattern for the next nested resource controller (tickets/messages, etc.).
/// </remarks>
public class CustomerContactsController : ApiControllerBase
{
    /// <summary>Lists a customer's contacts, primary first.</summary>
    [HttpGet("/api/customers/{customerId:guid}/contacts")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CustomerContactDto>>> GetList(
        Guid customerId, CancellationToken ct)
        => Ok(await Sender.Send(new GetCustomerContactsQuery(customerId), ct));

    /// <summary>Adds a contact. A cross-customer duplicate returns 409 unless confirmed.</summary>
    [HttpPost("/api/customers/{customerId:guid}/contacts")]
    [ProducesResponseType(typeof(CustomerContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerContactDto>> Add(
        Guid customerId, [FromBody] AddCustomerContactCommand command, CancellationToken ct)
    {
        var result = await Sender.Send(command with { CustomerId = customerId }, ct);
        return CreatedAtAction(nameof(GetList), new { customerId }, result);
    }

    /// <summary>Edits a contact's value or details.</summary>
    [HttpPut("/api/customers/{customerId:guid}/contacts/{id:guid}")]
    [ProducesResponseType(typeof(CustomerContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerContactDto>> Update(
        Guid customerId, Guid id, [FromBody] UpdateCustomerContactCommand command, CancellationToken ct)
        => Ok(await Sender.Send(command with { CustomerId = customerId, ContactId = id }, ct));

    /// <summary>Deletes a contact. Refused when it would leave the customer unreachable.</summary>
    [HttpDelete("/api/customers/{customerId:guid}/contacts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid customerId, Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteCustomerContactCommand(customerId, id), ct);
        return NoContent();
    }

    /// <summary>Promotes a contact to primary for its type, demoting the previous one.</summary>
    [HttpPost("/api/customers/{customerId:guid}/contacts/{id:guid}/primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(Guid customerId, Guid id, CancellationToken ct)
    {
        await Sender.Send(new SetPrimaryContactCommand(customerId, id), ct);
        return NoContent();
    }

    /// <summary>Sends a verification code for an email or mobile contact.</summary>
    [HttpPost("/api/customers/{customerId:guid}/contacts/{id:guid}/verify/send")]
    [ProducesResponseType(typeof(SendContactVerificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SendContactVerificationResultDto>> SendVerification(
        Guid customerId, Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new SendContactVerificationCommand(customerId, id), ct));

    /// <summary>Confirms a verification code.</summary>
    [HttpPost("/api/customers/{customerId:guid}/contacts/{id:guid}/verify/confirm")]
    [ProducesResponseType(typeof(ConfirmContactVerificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfirmContactVerificationResultDto>> ConfirmVerification(
        Guid customerId, Guid id, [FromBody] ConfirmVerificationRequest request, CancellationToken ct)
        => Ok(await Sender.Send(
            new ConfirmContactVerificationCommand { CustomerId = customerId, ContactId = id, Code = request.Code },
            ct));
}

public record ConfirmVerificationRequest(string Code);
