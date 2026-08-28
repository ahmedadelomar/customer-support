using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Adds timeline rows to the current unit of work. Nothing is saved here: the caller saves, so the
/// event and the state change it describes commit together or not at all.
/// </summary>
public class TicketEventRecorder(AppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : ITicketEventRecorder
{
    public void Record(
        Guid ticketId,
        TicketEventType eventType,
        string? field = null,
        string? oldValue = null,
        string? newValue = null,
        string? oldDisplay = null,
        string? newDisplay = null,
        string? metadataJson = null,
        string? triggeredByRule = null)
    {
        db.TicketEvents.Add(new TicketEvent
        {
            TicketId = ticketId,
            EventType = eventType,
            ActorId = currentUser.UserId,
            ActorDisplayName = currentUser.UserName,
            IsSystemGenerated = currentUser.UserId is null,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            OldDisplayValue = oldDisplay,
            NewDisplayValue = newDisplay,
            MetadataJson = metadataJson,
            TriggeredByRule = triggeredByRule,
            OccurredAt = clock.UtcNow,
        });
    }
}
