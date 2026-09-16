using CustomerSupport.Application.Auditing.Queries;
using CustomerSupport.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// The audit trail (Security &amp; Administration / Audit logs).
/// </summary>
/// <remarks>
/// Read-only by design: there is deliberately no update or delete action here, for anyone, including
/// a system administrator. Rows leave the table only through the nightly retention job.
/// </remarks>
[Route("api/audit-logs")]
public class AuditLogsController : ApiControllerBase
{
    /// <summary>Filtered, paged trail. Defaults to the last 7 days when no range is given.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogListItemDto>>> GetList(
        [FromQuery] GetAuditLogsQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));

    /// <summary>Distinct entity types present in the trail, for the filter dropdown.</summary>
    [HttpGet("entity-types")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetEntityTypes(CancellationToken ct)
        => Ok(await Sender.Send(new GetAuditEntityTypesQuery(), ct));

    /// <summary>Exports the filtered trail as CSV. Requires the export permission and is itself audited.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export([FromQuery] ExportAuditLogsQuery query, CancellationToken ct)
    {
        var csv = await Sender.Send(query, ct);
        return File(csv, "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    /// <summary>One entry with its field-level diff.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new GetAuditLogByIdQuery(id), ct));
}
