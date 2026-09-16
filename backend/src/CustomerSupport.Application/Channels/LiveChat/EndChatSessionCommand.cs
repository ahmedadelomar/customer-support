using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Ends a session — visitor or agent, either party per the story, so this carries no
/// <see cref="Common.Security.RequirePermissionAttribute"/>; the controller checks the caller holds
/// the session's own token or the agent permission before dispatching this.
/// </summary>
/// <remarks>
/// A session nobody ever accepted (still "Waiting" when the visitor leaves) is the story's offline
/// path: if the visitor identified themselves and said something, a ticket is raised from the
/// transcript so the message is not simply lost. An unidentified visitor has no customer to raise a
/// ticket for — the chat rows themselves are all that is kept, same as any other abandoned draft.
/// </remarks>
public record EndChatSessionCommand(Guid SessionId) : IRequest<ChatSessionDto>;

public class EndChatSessionCommandHandler(
    IAppDbContext db,
    IReferenceNumberGenerator numbers,
    ITicketEventRecorder events,
    IInteractionRecorder interactions,
    ISlaEngine sla,
    IAssignmentEngine assignment,
    IChatRealtimeNotifier realtime,
    IDateTimeProvider clock,
    ILogger<EndChatSessionCommandHandler> logger)
    : IRequestHandler<EndChatSessionCommand, ChatSessionDto>
{
    public async Task<ChatSessionDto> Handle(EndChatSessionCommand command, CancellationToken ct)
    {
        var session = await db.ChatSessions.Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException(nameof(ChatSession), command.SessionId);

        if (session.Status is "Ended" or "Abandoned")
        {
            return ChatMapper.ToDto(session, null, 0);
        }

        var wasNeverAccepted = session.Status == "Waiting";
        var messages = session.Messages.OrderBy(m => m.SentAt).ToList();

        session.Status = "Ended";
        session.EndedAt = clock.UtcNow;
        session.MessageCount = messages.Count;

        Guid? ticketId = null;
        if (wasNeverAccepted && session.CustomerId is { } customerId &&
            messages.Any(m => m.AuthorType == MessageAuthorType.Customer))
        {
            ticketId = await CreateOfflineTicketAsync(session, customerId, messages, ct);
            session.TicketId = ticketId;
        }

        if (session.CustomerId is { } known)
        {
            var preview = messages.Count > 0 ? Truncate(messages[^1].Body) : "(no messages)";
            interactions.Record(
                known, ChannelKey.LiveChat, MessageDirection.Inbound,
                "Live chat ended", preview, ticketId, nameof(ChatSession), session.Id);
        }

        await db.SaveChangesAsync(ct);

        if (ticketId is { } tid)
        {
            await sla.ApplyPolicyAsync(tid, ct);
            try
            {
                await assignment.AssignAsync(tid, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automatic assignment failed for offline-chat ticket {TicketId}.", tid);
            }
        }

        var dto = ChatMapper.ToDto(session, null, 0);
        await realtime.PushToSessionAsync(session.Id, "session.ended", dto, ct);
        if (session.QueuedForTeamId is { } teamId)
        {
            await realtime.PushToTeamQueueAsync(teamId, "session.ended", dto, ct);
        }

        return dto;
    }

    /// <summary>
    /// Builds the ticket directly rather than dispatching <c>CreateTicketCommand</c> — the caller
    /// here may be the anonymous visitor themselves, who holds no <c>tickets.create</c> permission
    /// for <c>AuthorizationBehaviour</c> to check. Mirrors <c>InboundMessagePipeline.CreateNewTicketAsync</c>'s
    /// same duplication of the default-resolution cascade, for the same reason.
    /// </summary>
    private async Task<Guid> CreateOfflineTicketAsync(
        ChatSession session, Guid customerId, IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket status is configured.");
        var priority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket priority is configured.");
        var category = await db.TicketCategories.Where(c => c.IsActive).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active ticket category is configured.");
        var department = await db.Departments.Where(d => d.IsActive && d.BranchId == session.BranchId)
            .OrderBy(d => d.CreatedAt).FirstOrDefaultAsync(ct)
            ?? await db.Departments.Where(d => d.IsActive).OrderBy(d => d.CreatedAt).FirstOrDefaultAsync(ct);

        var transcript = ChatTranscript.Render(messages);

        var ticket = new Ticket
        {
            Number = await numbers.NextTicketNumberAsync(ct),
            CustomerId = customerId,
            BranchId = session.BranchId,
            Subject = ChatTranscript.Subject(session, messages),
            Description = transcript,
            Language = session.Language,
            CategoryId = category.Id,
            PriorityId = priority.Id,
            StatusId = status.Id,
            Channel = ChannelKey.LiveChat,
            ChannelAccountId = session.ChannelAccountId,
            DepartmentId = department?.Id,
        };

        db.Tickets.Add(ticket);

        ticket.Messages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = ChannelKey.LiveChat,
            Direction = MessageDirection.Inbound,
            AuthorType = MessageAuthorType.Customer,
            AuthorId = customerId,
            AuthorDisplayName = session.VisitorName,
            Subject = ticket.Subject,
            BodyText = transcript,
            SentAt = clock.UtcNow,
        });

        events.Record(ticket.Id, TicketEventType.Created);

        return ticket.Id;
    }

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
