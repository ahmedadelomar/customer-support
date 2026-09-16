using CustomerSupport.Application.Channels.Sms;
using CustomerSupport.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>SMS-specific helpers used while composing a reply (Communication Channels / SMS channel, CS-304).</summary>
[Route("api/channels/sms")]
public class SmsController(ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost("segments")]
    [ProducesResponseType(typeof(SmsSegmentsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SmsSegmentsResult>> Segments([FromBody] SmsSegmentsRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new GetSmsSegmentsQuery(request.Text, currentUser.BranchId), ct));
}

public record SmsSegmentsRequest(string Text);
