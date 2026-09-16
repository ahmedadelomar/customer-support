using CustomerSupport.Application.Security.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// The permission catalogue (Security &amp; Administration / Permissions). Read-only by design:
/// permissions are declared in code and seeded from that registry — only the grants are editable.
/// </summary>
public class PermissionsController : ApiControllerBase
{
    /// <summary>Every permission, grouped by category, for the role matrix editor.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionCategoryDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetPermissionsQuery(), ct));
}
