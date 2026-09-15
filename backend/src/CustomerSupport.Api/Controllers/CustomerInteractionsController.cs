using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Application.Customers.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Read-only unified interaction timeline, nested under a customer (Customer Management /
/// Interaction history). Deliberately has no POST/PUT/DELETE action — see the class remark on
/// <see cref="Domain.Customers.Interaction"/>: the timeline is a projection, never edited by hand.
/// </summary>
/// <remarks>
/// Uses the app-relative route template pattern established by <c>CustomerContactsController</c> —
/// see its remarks for why.
/// </remarks>
public class CustomerInteractionsController : ApiControllerBase
{
    /// <summary>Keyset-paged timeline, newest first. See <see cref="GetInteractionsQuery"/> for the cursor shape.</summary>
    [HttpGet("/api/customers/{customerId:guid}/interactions")]
    [ProducesResponseType(typeof(InteractionPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InteractionPageDto>> GetList(
        Guid customerId,
        [FromQuery] GetInteractionsQuery query,
        CancellationToken ct)
        => Ok(await Sender.Send(query with { CustomerId = customerId }, ct));
}
