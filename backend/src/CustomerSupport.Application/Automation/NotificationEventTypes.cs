namespace CustomerSupport.Application.Automation;

/// <summary>Groups event types on the preferences matrix so it reads as sections, not thirty flat rows.</summary>
public enum NotificationArea
{
    Tickets,
    Sla,
    Tasks,
    Collaboration,
}

/// <summary>
/// One row of the notification preferences matrix, and the single source of the documented system
/// default for that event — read by both <c>NotificationDispatcher</c> (falls back to this when a
/// user has no saved preference row) and the preferences screen (shows it as "default" until the
/// user overrides it), so the two can never disagree about what "the default" means.
/// </summary>
public record NotificationEventTypeInfo(
    string Key, NotificationArea Area, bool DefaultViaEmail, bool DefaultViaSms, bool DefaultViaPush);

/// <summary>
/// Every event type the system actually dispatches. Deliberately a fixed list rather than
/// discovered from call sites — an event type that stops firing should stop being configurable, not
/// linger as a dead row a user configured for nothing.
/// </summary>
public static class NotificationEventTypes
{
    public static IReadOnlyList<NotificationEventTypeInfo> All { get; } =
    [
        new("ticket.assigned", NotificationArea.Tickets, DefaultViaEmail: true, DefaultViaSms: false, DefaultViaPush: false),
        new("ticket.unassigned", NotificationArea.Tickets, DefaultViaEmail: false, DefaultViaSms: false, DefaultViaPush: false),
        new("ticket.transferred", NotificationArea.Tickets, DefaultViaEmail: false, DefaultViaSms: false, DefaultViaPush: false),
        new("ticket.mentioned", NotificationArea.Collaboration, DefaultViaEmail: true, DefaultViaSms: false, DefaultViaPush: false),
        new("ticket.escalated", NotificationArea.Sla, DefaultViaEmail: true, DefaultViaSms: false, DefaultViaPush: false),
        new("sla.warning", NotificationArea.Sla, DefaultViaEmail: false, DefaultViaSms: false, DefaultViaPush: false),
        new("sla.breached", NotificationArea.Sla, DefaultViaEmail: true, DefaultViaSms: false, DefaultViaPush: false),
        new("task.assigned", NotificationArea.Tasks, DefaultViaEmail: false, DefaultViaSms: false, DefaultViaPush: false),
        new("reminder.due", NotificationArea.Tasks, DefaultViaEmail: false, DefaultViaSms: false, DefaultViaPush: false),
    ];

    public static NotificationEventTypeInfo? Find(string eventType) => All.FirstOrDefault(e => e.Key == eventType);
}
