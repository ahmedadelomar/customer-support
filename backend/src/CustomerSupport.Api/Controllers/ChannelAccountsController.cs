using CustomerSupport.Application.Channels.ChannelAccounts;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Channel account administration (Communication Channels / Email channel and beyond — every inbound channel shares this one table).</summary>
public class ChannelAccountsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChannelAccountDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetChannelAccountsQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Create([FromBody] ChannelAccountRequest request, CancellationToken ct)
    {
        var id = await Sender.Send(new CreateChannelAccountCommand(request), ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ChannelAccountRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateChannelAccountCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteChannelAccountCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(TestChannelAccountConnectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestChannelAccountConnectionResult>> Test(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new TestChannelAccountConnectionCommand(id), ct));
}
