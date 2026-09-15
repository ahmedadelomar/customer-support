using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Domain.Workspace;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>
/// Turns the structured mentions inside a just-added note/message into <see cref="TicketMention"/>
/// rows, adds each mentioned colleague as a watcher when they are not already one, and queues their
/// notification — the three effects the story requires from a single "@" mention, applied wherever a
/// note or handover message is written (<c>AddInternalNoteCommand</c>, a handover note from
/// <c>AssignTicketCommand</c>). The caller saves; this only stages.
/// </summary>
public static class MentionProcessor
{
    public static async Task ProcessAsync(
        IAppDbContext db,
        INotificationDispatcher notifications,
        ITicketEventRecorder events,
        Ticket ticket,
        TicketMessage message,
        Guid authorId,
        string authorDisplayName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var mentions = MentionParser.Parse(message.BodyText);
        if (mentions.Count == 0)
        {
            return;
        }

        var existingWatcherIds = ticket.Watchers.Select(w => w.UserId).ToHashSet();

        foreach (var mention in mentions)
        {
            if (mention.UserId == authorId)
            {
                continue;
            }

            db.TicketMentions.Add(new TicketMention
            {
                TicketId = ticket.Id,
                TicketMessageId = message.Id,
                MentionedUserId = mention.UserId,
                MentionedById = authorId,
                MentionedAt = now,
            });

            if (existingWatcherIds.Add(mention.UserId))
            {
                ticket.Watchers.Add(new TicketWatcher
                {
                    TicketId = ticket.Id,
                    UserId = mention.UserId,
                    AddedByAutomation = false,
                    AddedById = authorId,
                    AddedAt = now,
                });

                events.Record(ticket.Id, TicketEventType.WatcherAdded,
                    newValue: mention.UserId.ToString(), newDisplay: mention.DisplayName,
                    metadataJson: """{"reason":"mention"}""");
            }

            await notifications.DispatchAsync(
                mention.UserId,
                "ticket.mentioned",
                "You were mentioned in a ticket",
                "تمت الإشارة إليك في تذكرة",
                $"{authorDisplayName} mentioned you on ticket {ticket.Number}: {ticket.Subject}",
                $"أشار إليك {authorDisplayName} في التذكرة {ticket.Number}: {ticket.Subject}",
                link: $"/agent/tickets/{ticket.Id}",
                ct: ct);
        }
    }
}
