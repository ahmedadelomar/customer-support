using CustomerSupport.Application.Channels.WebForms;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Web form definition administration and submissions (Communication Channels / Web forms, CS-305).</summary>
[Route("api/web-forms")]
public class WebFormsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WebFormDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WebFormDefinitionDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetWebFormsQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Create([FromBody] WebFormDefinitionRequest request, CancellationToken ct)
    {
        var id = await Sender.Send(new CreateWebFormCommand(request), ct);
        return CreatedAtAction(nameof(GetList), null, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] WebFormDefinitionRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateWebFormCommand(id, request), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/submissions")]
    [ProducesResponseType(typeof(IReadOnlyList<WebFormSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WebFormSubmissionDto>>> GetSubmissions(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new GetWebFormSubmissionsQuery(id), ct));

    [HttpPost("submissions/{id:guid}/retry")]
    [ProducesResponseType(typeof(Guid?), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid?>> Retry(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new RetryWebFormSubmissionCommand(id), ct));
}
