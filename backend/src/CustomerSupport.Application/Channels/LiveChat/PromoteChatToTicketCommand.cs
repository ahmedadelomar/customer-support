using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Commands;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Promotes a chat to a ticket. Unlike the offline path in <c>EndChatSessionCommand</c>, the caller
/// here is a real, permission-bearing agent, so this dispatches the normal
/// <see cref="CreateTicketCommand"/> through MediatR and gets its category/priority/department
/// defaults, SLA clock and automatic assignment for free rather than a third copy of that cascade.
/// </summary>
[RequirePermission(Permissions.Tickets.Create)]
public record PromoteChatToTicketCommand(Guid SessionId, Guid CategoryId, Guid? PriorityId, Guid? DepartmentId)
    : IRequest<Guid>;

public class PromoteChatToTicketCommandHandler(
    IAppDbContext db, ISender sender, IInteractionRecorder interactions,
    IChatRealtimeNotifier realtime, IDateTimeProvider clock)
    : IRequestHandler<PromoteChatToTicketCommand, Guid>
{
    public async Task<Guid> Handle(PromoteChatToTicketCommand command, CancellationToken ct)
    {
        var session = await db.ChatSessions.Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException(nameof(ChatSession), command.SessionId);

        if (session.TicketId is not null)
        {
            throw new ConflictException("This chat has already been promoted to a ticket.");
        }

        if (session.CustomerId is not { } customerId)
        {
            throw new ConflictException("Identify the visitor before promoting this chat to a ticket.");
        }

        var messages = session.Messages.OrderBy(m => m.SentAt).ToList();
        var transcript = ChatTranscript.Render(messages);

        var ticketId = await sender.Send(new CreateTicketCommand
        {
            CustomerId = customerId,
            Subject = ChatTranscript.Subject(session, messages),
            Description = transcript,
            CategoryId = command.CategoryId,
            PriorityId = command.PriorityId,
            DepartmentId = command.DepartmentId,
            Channel = ChannelKey.LiveChat,
        }, ct);

        session.TicketId = ticketId;
        if (session.Status is not ("Ended" or "Abandoned"))
        {
            session.Status = "Ended";
            session.EndedAt = clock.UtcNow;
            session.MessageCount = messages.Count;
        }

        interactions.Record(
            customerId, ChannelKey.LiveChat, MessageDirection.Inbound,
            "Live chat promoted to ticket", ChatTranscript.Subject(session, messages), ticketId, nameof(ChatSession), session.Id);

        await db.SaveChangesAsync(ct);

        var dto = ChatMapper.ToDto(session, null, 0);
        await realtime.PushToSessionAsync(session.Id, "session.promoted", dto, ct);
        if (session.QueuedForTeamId is { } teamId)
        {
            await realtime.PushToTeamQueueAsync(teamId, "session.promoted", dto, ct);
        }

        return ticketId;
    }
}
