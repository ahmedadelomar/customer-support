using CustomerSupport.Api.Infrastructure;
using CustomerSupport.Application.Channels.WebForms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// The public side of web forms (Communication Channels / Web forms, CS-305) — anonymous and, per
/// the story, the most exposed endpoint in the product. The per-form-per-IP check inside
/// <c>SubmitWebFormCommand</c> is the precise limit; <see cref="RateLimitPolicies.WebFormSubmit"/>
/// here is the coarse second layer the story calls for, since the database check itself costs a query.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/forms")]
[Produces("application/json")]
public class PublicWebFormsController(ISender sender) : ControllerBase
{
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(PublicWebFormSchemaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicWebFormSchemaDto>> GetSchema(string key, CancellationToken ct)
        => Ok(await sender.Send(new GetPublicWebFormSchemaQuery(key), ct));

    [HttpPost("{key}/submit")]
    [EnableRateLimiting(RateLimitPolicies.WebFormSubmit)]
    [ProducesResponseType(typeof(SubmitWebFormResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitWebFormResult>> Submit(
        string key, [FromBody] SubmitWebFormRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await sender.Send(new SubmitWebFormCommand(key, request, ip, userAgent), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
