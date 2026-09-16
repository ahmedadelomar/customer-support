using CustomerSupport.Application.Automation.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>The signed-in user's own notification centre (SLA and Automation / Alerts and notifications).</summary>
public class NotificationsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetList(
        [FromQuery] int skip, [FromQuery] int take, CancellationToken ct)
        => Ok(await Sender.Send(new GetNotificationsQuery(skip, take == 0 ? 30 : take), ct));

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
        => Ok(await Sender.Send(new GetUnreadNotificationCountQuery(), ct));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await Sender.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await Sender.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }
}

/// <summary>The caller's notification channel matrix. Separate controller so the route matches the plan's <c>/api/notification-preferences</c>.</summary>
[Route("api/notification-preferences")]
public class NotificationPreferencesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceDto>>> GetList(CancellationToken ct)
        => Ok(await Sender.Send(new GetNotificationPreferencesQuery(), ct));

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateNotificationPreferencesCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }
}
