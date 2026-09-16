using CustomerSupport.Application.Channels.Sms;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>SMS-specific helpers used while composing a reply (Communication Channels / SMS channel, CS-304).</summary>
[Route("api/channels/sms")]
public class SmsController : ApiControllerBase
{
    [HttpPost("segments")]
    [ProducesResponseType(typeof(SmsSegments), StatusCodes.Status200OK)]
    public async Task<ActionResult<SmsSegments>> Segments([FromBody] SmsSegmentsRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new GetSmsSegmentsQuery(request.Text), ct));
}

public record SmsSegmentsRequest(string Text);
