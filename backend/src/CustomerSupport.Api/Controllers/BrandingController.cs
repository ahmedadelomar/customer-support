using CustomerSupport.Application.Branding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Runtime theming (Platform / Runtime branding and theming). The read is anonymous because the
/// portal applies the theme before anyone signs in; every write requires <c>admin.branding.manage</c>.
/// </summary>
public class BrandingController : ApiControllerBase
{
    /// <summary>Resolved branding for a branch, falling back to the global row and then the defaults.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BrandingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BrandingDto>> Get([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await Sender.Send(new GetBrandingQuery(branchId), ct));

    /// <summary>Writes the theme for a branch, or the global default when no branch is given.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateBrandingCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Uploads a logo and returns its stored URL. PNG, JPEG and WebP only — SVG is refused.</summary>
    [HttpPost("logo")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<string>> UploadLogo(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();

        var url = await Sender.Send(
            new UploadBrandingLogoCommand(stream, file.FileName, file.ContentType, file.Length), ct);

        return Ok(url);
    }

    /// <summary>Removes a branch override, reverting that branch to the global theme.</summary>
    [HttpDelete("override")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride([FromQuery] Guid branchId, CancellationToken ct)
    {
        await Sender.Send(new DeleteBrandingOverrideCommand(branchId), ct);
        return NoContent();
    }
}
