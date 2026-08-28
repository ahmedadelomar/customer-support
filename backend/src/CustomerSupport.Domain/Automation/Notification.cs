using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Automation;

/// <summary>
/// An in-app notification for one user (SLA and Automation / Alerts and notifications).
/// Bilingual text is rendered at write time so the record stays readable regardless of later
/// template changes; fan-out to email, SMS and push is driven by NotificationPreference.
/// </summary>
public class Notification : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Event key such as ticket.assigned or sla.breached, matched against preferences.</summary>
    public string EventType { get; set; } = string.Empty;
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Body { get; set; } = new();

    /// <summary>Relative in-app route the notification opens.</summary>
    public string? Link { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }

    /// <summary>Info, Warning or Critical. Drives icon and colour, and overrides quiet hours when Critical.</summary>
    public string Severity { get; set; } = "Info";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>Channels the fan-out actually dispatched on, for troubleshooting.</summary>
    public string? DispatchedChannels { get; set; }
}
