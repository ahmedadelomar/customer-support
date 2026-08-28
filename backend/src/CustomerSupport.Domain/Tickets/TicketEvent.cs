using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Tickets;

/// <summary>
/// Append-only ticket timeline row (Ticket Management / Ticket history).
/// Written by <c>ITicketEventRecorder</c> inside the same transaction as the mutation it describes,
/// so the history can never drift from the ticket state.
/// </summary>
public class TicketEvent : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public TicketEventType EventType { get; set; }

    /// <summary>Null for automation-generated events; see <see cref="IsSystemGenerated"/>.</summary>
    public Guid? ActorId { get; set; }
    public string? ActorDisplayName { get; set; }
    public bool IsSystemGenerated { get; set; }

    /// <summary>Entity property that changed, for example <c>StatusId</c>.</summary>
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>Human-readable labels captured at write time, so history survives later lookup renames.</summary>
    public string? OldDisplayValue { get; set; }
    public string? NewDisplayValue { get; set; }

    /// <summary>Optional JSON payload for events that do not reduce to a single field change.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>Name of the rule that caused this event, when automation rather than a person did it.</summary>
    public string? TriggeredByRule { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
