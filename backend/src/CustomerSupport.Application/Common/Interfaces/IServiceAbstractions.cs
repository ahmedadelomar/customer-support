using CustomerSupport.Domain.Common;
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

/// <summary>
/// Appends a row to the unified customer timeline (Customer Management / Interaction history).
/// Every feature that represents a customer touchpoint — ticket creation, an inbound or outbound
/// message, a chat session, a portal submission, a CSAT response — must call this in the SAME
/// transaction as the row it describes, or the timeline silently falls out of sync with reality.
/// </summary>
public interface IInteractionRecorder
{
    /// <summary>Adds a timeline entry to the current unit of work. The caller saves.</summary>
    void Record(
        Guid customerId,
        ChannelKey channel,
        MessageDirection direction,
        string? subject,
        string? preview,
        Guid? ticketId = null,
        string? sourceType = null,
        Guid? sourceId = null,
        Guid? agentId = null);
}

/// <summary>
/// Resolves display names for a batch of user ids. Exists so Application-layer handlers can show
/// "who did this" (an interaction's agent, a ticket's assignee, an audit actor, …) without depending
/// on the Identity types that live in Infrastructure — <see cref="IAppDbContext"/> deliberately does
/// not expose <c>ApplicationUser</c>, to keep that boundary real rather than aspirational.
/// </summary>
public interface IUserDisplayNameResolver
{
    Task<IReadOnlyDictionary<Guid, LocalizedText>> ResolveAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default);
}

/// <summary>The agent fields assignment and capacity checks need, read without exposing <c>ApplicationUser</c> itself.</summary>
public record AgentSnapshot(
    Guid Id,
    LocalizedText DisplayName,
    bool IsActive,
    string AvailabilityStatus,
    int MaxConcurrentTickets,
    Guid? DepartmentId);

/// <summary>
/// Resolves agent identity and availability for assignment (Ticket Management / Assign tickets to
/// agents). Same reasoning as <see cref="IUserDisplayNameResolver"/>: <see cref="IAppDbContext"/>
/// never exposes <c>ApplicationUser</c>, so handlers needing more than a name go through this instead.
/// </summary>
public interface IAgentDirectory
{
    Task<AgentSnapshot?> GetAsync(Guid agentId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, AgentSnapshot>> GetManyAsync(IEnumerable<Guid> agentIds, CancellationToken ct = default);
    /// <summary>Active agents whose home department matches — the fallback candidate pool when a ticket has no team yet.</summary>
    Task<IReadOnlyList<Guid>> GetAgentIdsByDepartmentAsync(Guid departmentId, CancellationToken ct = default);
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

/// <summary>The size cap and extension allow-list an upload is checked against.</summary>
public record AttachmentPolicy(long MaxBytes, IReadOnlyCollection<string> AllowedExtensions);

/// <summary>
/// Source of <see cref="AttachmentPolicy"/>. Reads plain configuration today — CS-1004 (system
/// configuration) is not built yet, so there is nowhere to read a <c>SystemSetting</c> row from.
/// The method is async so that swapping the implementation for one backed by the database, once
/// CS-1004 lands, needs no signature change and no change to any caller.
/// </summary>
public interface IAttachmentPolicyProvider
{
    Task<AttachmentPolicy> GetPolicyAsync(CancellationToken ct = default);
}

/// <summary>
/// Scans a saved file before it becomes downloadable. The default implementation is a no-op that
/// reports <c>"skipped"</c> — shipping the hook now means wiring a real scanner later is
/// configuration, not a schema or call-site change.
/// </summary>
public interface IVirusScanner
{
    /// <summary>Returns <c>"skipped"</c>, <c>"clean"</c> or <c>"infected"</c>.</summary>
    Task<string> ScanAsync(string storageKey, CancellationToken ct = default);
}
