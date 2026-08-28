using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Automation;

/// <summary>
/// Per-user, per-event opt-in matrix. A missing row means the system default for that event applies.
/// </summary>
public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;

    public bool ViaInApp { get; set; } = true;
    public bool ViaEmail { get; set; }
    public bool ViaSms { get; set; }
    public bool ViaPush { get; set; }

    /// <summary>Start of a quiet window during which only critical alerts are delivered.</summary>
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
}
