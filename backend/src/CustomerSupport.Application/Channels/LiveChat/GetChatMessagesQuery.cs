using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// No <see cref="Common.Security.RequirePermissionAttribute"/> — the controller checks the caller
/// holds this session's own token or the agent permission before ever dispatching this, exactly as
/// for <c>EndChatSessionCommand</c>.
/// </summary>
public record GetChatMessagesQuery(Guid SessionId) : IRequest<IReadOnlyList<ChatMessageDto>>;

public class GetChatMessagesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    public async Task<IReadOnlyList<ChatMessageDto>> Handle(GetChatMessagesQuery request, CancellationToken ct)
    {
        var exists = await db.ChatSessions.AnyAsync(s => s.Id == request.SessionId, ct);
        if (!exists)
        {
            throw new NotFoundException(nameof(ChatSession), request.SessionId);
        }

        var messages = await db.ChatMessages
            .Where(m => m.ChatSessionId == request.SessionId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

        return messages.Select(ChatMapper.ToDto).ToList();
    }
}
