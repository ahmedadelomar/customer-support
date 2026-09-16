using CustomerSupport.Application.Automation;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Cross-cutting automation endpoints shared by assignment (CS-502) and escalation (CS-503) rules:
/// the allow-listed condition field list the rule builders fetch, and the decision log viewer.
/// </summary>
public class AutomationController : ApiControllerBase
{
    /// <summary>The exact field names the condition builder evaluates against, so the UI can never drift from the allow-list.</summary>
    [HttpGet("condition-fields")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetConditionFields() => Ok(ConditionFields.All);

    [HttpGet("log")]
    [ProducesResponseType(typeof(IReadOnlyList<AutomationRunLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AutomationRunLogDto>>> GetLog([FromQuery] GetAutomationRunLogsQuery query, CancellationToken ct)
        => Ok(await Sender.Send(query, ct));
}
