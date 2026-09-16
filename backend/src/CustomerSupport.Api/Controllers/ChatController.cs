using CustomerSupport.Api.Chat;
using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// Live chat (Communication Channels / Live chat, CS-303). Most actions carry no
/// <c>[RequirePermission]</c> in the MediatR pipeline because the caller may be an anonymous
/// visitor — <see cref="EnsureSessionAccess"/> is what actually gates them, checked here before
/// dispatch, the same rule <c>ChatHub</c> applies to its own methods.
/// </summary>
[Route("api/chat")]
public class ChatController : ApiControllerBase
{
    [HttpPost("sessions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StartChatSessionResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<StartChatSessionResult>> StartSession(
        [FromBody] StartChatSessionRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await Sender.Send(new StartChatSessionCommand(request, ip, userAgent), ct);
        return CreatedAtAction(nameof(GetMessages), new { id = result.SessionId }, result);
    }

    [HttpGet("sessions/{id:guid}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(Guid id, CancellationToken ct)
    {
        EnsureSessionAccess(id);
        return Ok(await Sender.Send(new GetChatMessagesQuery(id), ct));
    }

    [HttpPost("sessions/{id:guid}/identify")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatSessionDto>> Identify(
        Guid id, [FromBody] IdentifyChatSessionRequest request, CancellationToken ct)
    {
        EnsureSessionAccess(id);
        return Ok(await Sender.Send(new IdentifyChatSessionCommand(id, request), ct));
    }

    [HttpPost("sessions/{id:guid}/end")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatSessionDto>> End(Guid id, CancellationToken ct)
    {
        EnsureSessionAccess(id);
        return Ok(await Sender.Send(new EndChatSessionCommand(id), ct));
    }

    [HttpPost("sessions/{id:guid}/rate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Rate(Guid id, [FromBody] RateChatSessionRequest request, CancellationToken ct)
    {
        EnsureSessionAccess(id);
        await Sender.Send(new RateChatSessionCommand(id, request), ct);
        return NoContent();
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(ChatQueueDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatQueueDto>> GetQueue(CancellationToken ct)
        => Ok(await Sender.Send(new GetChatQueueQuery(), ct));

    [HttpPost("sessions/{id:guid}/accept")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChatSessionDto>> Accept(Guid id, CancellationToken ct)
        => Ok(await Sender.Send(new AcceptChatSessionCommand(id), ct));

    [HttpPost("sessions/{id:guid}/promote")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<ActionResult<Guid>> Promote(
        Guid id, [FromBody] PromoteChatToTicketRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new PromoteChatToTicketCommand(id, request.CategoryId, request.PriorityId, request.DepartmentId), ct));

    /// <summary>A visitor may act on exactly the session its own token names; an agent may act on any session once they hold the handle-chat permission.</summary>
    private void EnsureSessionAccess(Guid sessionId)
    {
        if (!ChatAccess.CanAccess(User, sessionId))
        {
            throw new ForbiddenException("You do not have access to this chat session.");
        }
    }
}

public record PromoteChatToTicketRequest(Guid CategoryId, Guid? PriorityId, Guid? DepartmentId);
