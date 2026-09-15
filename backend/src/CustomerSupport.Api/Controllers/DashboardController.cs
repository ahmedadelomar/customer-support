using CustomerSupport.Application.AgentDashboard.Dtos;
using CustomerSupport.Application.AgentDashboard.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>Agent home screen (Agent Dashboard / Assigned tickets).</summary>
public class DashboardController : ApiControllerBase
{
    /// <summary>Tiles, next-up queue and personal stats in one response. Open to any authenticated caller.</summary>
    [HttpGet("agent")]
    [ProducesResponseType(typeof(AgentDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentDashboardDto>> GetAgentDashboard(CancellationToken ct)
        => Ok(await Sender.Send(new GetAgentDashboardQuery(), ct));

    /// <summary>Just the next-up queue, for the auto-refresh poll.</summary>
    [HttpGet("agent/queue")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentQueueItemDto>>> GetAgentQueue(CancellationToken ct)
        => Ok(await Sender.Send(new GetAgentQueueQuery(), ct));
}
