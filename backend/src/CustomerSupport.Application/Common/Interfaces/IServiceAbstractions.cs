using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>Allocates the human-readable reference numbers shown to customers.</summary>
public interface IReferenceNumberGenerator
{
    /// <summary>Returns the next ticket number, for example <c>TCK-2026-000123</c>.</summary>
    Task<string> NextTicketNumberAsync(CancellationToken ct = default);

    /// <summary>Returns the next customer code, for example <c>CUS-000123</c>.</summary>
    Task<string> NextCustomerCodeAsync(CancellationToken ct = default);
}

/// <summary>
/// Appends rows to the ticket timeline. Every command that mutates a ticket must call this so
/// Ticket history can never fall out of sync with ticket state.
/// </summary>
public interface ITicketEventRecorder
{
    void Record(
        Guid ticketId,
        TicketEventType eventType,
        string? field = null,
        string? oldValue = null,
        string? newValue = null,
        string? oldDisplay = null,
        string? newDisplay = null,
        string? metadataJson = null,
        string? triggeredByRule = null);
}

/// <summary>Working-hours arithmetic used by every SLA calculation.</summary>
public interface IBusinessCalendarCalculator
{
    /// <summary>Adds working minutes to an instant, skipping closed hours and holidays.</summary>
    Task<DateTimeOffset> AddWorkingMinutesAsync(Guid calendarId, DateTimeOffset from, int minutes, CancellationToken ct = default);

    /// <summary>Working minutes elapsed between two instants.</summary>
    Task<int> WorkingMinutesBetweenAsync(Guid calendarId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

/// <summary>Selects and applies the SLA policy for a ticket and maintains its clocks.</summary>
public interface ISlaEngine
{
    Task ApplyPolicyAsync(Guid ticketId, CancellationToken ct = default);
    Task OnFirstAgentReplyAsync(Guid ticketId, CancellationToken ct = default);
    Task OnStatusChangedAsync(Guid ticketId, CancellationToken ct = default);
    Task OnResolvedAsync(Guid ticketId, CancellationToken ct = default);
}

/// <summary>Evaluates assignment rules and picks the agent for a ticket.</summary>
public interface IAssignmentEngine
{
    /// <summary>Returns the chosen agent, or null when the ticket should stay in a team queue.</summary>
    Task<Guid?> AssignAsync(Guid ticketId, CancellationToken ct = default);
}

/// <summary>Fan-out for alerts: writes the in-app row then dispatches to the opted-in channels.</summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(
        Guid userId,
        string eventType,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        string severity = "Info",
        CancellationToken ct = default);
}

/// <summary>Binary storage for attachments, backed by local disk in development and blob storage in production.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
