using CustomerSupport.Application.Security.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Roles and their permission grants (Security &amp; Administration / Permissions). System roles are
/// protected from rename and deletion, but their grants remain editable — an organisation may
/// legitimately want a narrower Agent role.
/// </summary>
public class RolesController : ApiControllerBase
{
    /// <summary>Every role with its granted permissions and how many users hold it.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetRolesQuery(), ct));
}
