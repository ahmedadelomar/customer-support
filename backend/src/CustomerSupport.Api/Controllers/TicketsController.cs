using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Tickets.Commands;
using CustomerSupport.Application.Tickets.Dtos;
using CustomerSupport.Application.Tickets.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Ticket creation, list, detail and conversation (Ticket Management / Create and track tickets).
/// Follows the shape established by <c>CustomersController</c>.
/// </summary>
public class TicketsController : ApiControllerBase
{
    /// <summary>Returns a paged, filterable list of tickets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> GetList(
        [FromQuery] GetTicketsQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>KPI tile counts for the list, scoped identically to it.</summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(TicketStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketStatisticsDto>> GetStatistics(
        [FromQuery] GetTicketStatisticsQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>Categories, priorities, statuses and departments for the create form's pickers and the list's filters.</summary>
    [HttpGet("lookups")]
    [ProducesResponseType(typeof(TicketLookupsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketLookupsDto>> GetLookups(CancellationToken ct)
        => Ok(await Sender.Send(new GetTicketLookupsQuery(), ct));

    /// <summary>The caller's saved list filters plus their team's shared ones.</summary>
    [HttpGet("views")]
    [ProducesResponseType(typeof(IReadOnlyList<SavedTicketViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SavedTicketViewDto>>> GetViews(CancellationToken ct)
        => Ok(await Sender.Send(new GetSavedTicketViewsQuery(), ct));

    /// <summary>Saves the current list query params as a named, reusable view.</summary>
    [HttpPost("views")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateView(
        [FromBody] CreateSavedTicketViewCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetViews), new { }, id);
    }

    /// <summary>Deletes a saved view. Only its owner may delete it.</summary>
    [HttpDelete("views/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteView(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteSavedTicketViewCommand(id), ct);
        return NoContent();
    }

    /// <summary>Returns one ticket, its customer summary and properties.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new GetTicketByIdQuery(id), ct));

    /// <summary>Paged conversation thread, oldest first.</summary>
    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(PagedResult<TicketMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TicketMessageDto>>> GetMessages(
        Guid id, [FromQuery] GetTicketMessagesQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query with { TicketId = id }, ct));

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTicketCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Edits subject, description, category, priority and tags.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "The route id and the body id must match." });
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Sends an outbound reply to the customer.</summary>
    [HttpPost("{id:guid}/reply")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Reply(
        Guid id, [FromBody] ReplyToTicketCommand command, CancellationToken ct)
    {
        var messageId = await Sender.Send(command with { TicketId = id }, ct);
        return CreatedAtAction(nameof(GetMessages), new { id }, messageId);
    }

    /// <summary>Adds an agent-only note. Never sent outbound, never shown in the portal.</summary>
    [HttpPost("{id:guid}/note")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> AddNote(
        Guid id, [FromBody] AddInternalNoteCommand command, CancellationToken ct)
    {
        var messageId = await Sender.Send(command with { TicketId = id }, ct);
        return CreatedAtAction(nameof(GetMessages), new { id }, messageId);
    }

    /// <summary>Merges this ticket into another. Refused for a self-merge or (without an override) a cross-customer merge.</summary>
    [HttpPost("{id:guid}/merge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Merge(
        Guid id, [FromBody] MergeTicketsCommand command, CancellationToken ct)
    {
        await Sender.Send(command with { TicketId = id }, ct);
        return NoContent();
    }
}
