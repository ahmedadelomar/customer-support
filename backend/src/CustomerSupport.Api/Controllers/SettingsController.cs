using CustomerSupport.Application.Settings.Commands;
using CustomerSupport.Application.Settings.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Runtime configuration (Security &amp; Administration / System configuration).
/// Secrets are write-only: reads return a mask, and resubmitting that mask leaves the value alone.
/// </summary>
public class SettingsController : ApiControllerBase
{
    /// <summary>Every setting grouped by category, resolved for the caller's branch.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SettingCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SettingCategoryDto>>> GetList(
        [FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await Sender.Send(new GetSettingsQuery(branchId), ct));

    /// <summary>The allow-listed subset the portal reads before anyone signs in.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, string?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyDictionary<string, string?>>> GetPublic(CancellationToken ct)
        => Ok(await Sender.Send(new GetPublicSettingsQuery(), ct));

    /// <summary>Writes one setting, globally or as a branch override.</summary>
    [HttpPut("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        await Sender.Send(new UpdateSettingCommand(key, request.Value, request.BranchId), ct);
        return NoContent();
    }

    /// <summary>Removes a branch override, restoring the inherited value.</summary>
    [HttpDelete("{key}/override")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride(string key, [FromQuery] Guid branchId, CancellationToken ct)
    {
        await Sender.Send(new DeleteSettingOverrideCommand(key, branchId), ct);
        return NoContent();
    }
}

public record UpdateSettingRequest(string? Value, Guid? BranchId = null);
