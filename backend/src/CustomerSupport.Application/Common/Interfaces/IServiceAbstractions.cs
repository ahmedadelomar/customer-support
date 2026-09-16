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

/// <summary>
/// The agent fields assignment and capacity checks need, read without exposing <c>ApplicationUser</c>
/// itself. <see cref="JobTitle"/> is used only by the quick-reply placeholder resolver's
/// <c>agent.jobTitle</c> token (Agent Dashboard / Quick replies).
/// </summary>
public record AgentSnapshot(
    Guid Id,
    LocalizedText DisplayName,
    bool IsActive,
    string AvailabilityStatus,
    int MaxConcurrentTickets,
    Guid? DepartmentId,
    string? JobTitle = null);

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

    /// <summary>
    /// Active agents whose display name matches <paramref name="search"/> (either language, case
    /// insensitive), for the mention "@" picker — deliberately not scoped to a team or department,
    /// since mentioning a colleague outside either is valid (it just triggers the visibility warning).
    /// </summary>
    Task<IReadOnlyList<AgentSnapshot>> SearchActiveAsync(string? search, int limit, CancellationToken ct = default);

    /// <summary>
    /// Of the given user ids, returns the ones holding <paramref name="permission"/> through any role
    /// they hold. Lives here (Infrastructure) rather than as an <see cref="ICurrentUser"/> method
    /// because it must answer for an ARBITRARY user, not just the caller — needed to tell whether a
    /// mentioned colleague can see a ticket via `tickets.view.all` without exposing role/permission
    /// tables through <c>IAppDbContext</c>.
    /// </summary>
    Task<IReadOnlySet<Guid>> FilterByPermissionAsync(IEnumerable<Guid> userIds, string permission, CancellationToken ct = default);
}

/// <summary>Working-hours arithmetic used by every SLA calculation.</summary>
public interface IBusinessCalendarCalculator
{
    /// <summary>Adds working minutes to an instant, skipping closed hours and holidays.</summary>
    Task<DateTimeOffset> AddWorkingMinutesAsync(Guid calendarId, DateTimeOffset from, int minutes, CancellationToken ct = default);

    /// <summary>Working minutes elapsed between two instants.</summary>
    Task<int> WorkingMinutesBetweenAsync(Guid calendarId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

/// <summary>
/// Drops a calendar's cached hours and holidays. <see cref="IBusinessCalendarCalculator"/> caches
/// calendars aggressively (it runs on every ticket create, status change and breach-sweep tick), so
/// the calendar admin commands call this the moment a calendar's hours or holidays change — otherwise
/// an edit would silently not take effect for up to the cache's lifetime.
/// </summary>
public interface IBusinessCalendarCacheInvalidator
{
    void Invalidate(Guid calendarId);
}

/// <summary>Selects and applies the SLA policy for a ticket and maintains its clocks.</summary>
public interface ISlaEngine
{
    Task ApplyPolicyAsync(Guid ticketId, CancellationToken ct = default);
    Task OnFirstAgentReplyAsync(Guid ticketId, CancellationToken ct = default);
    Task OnStatusChangedAsync(Guid ticketId, CancellationToken ct = default);
    Task OnResolvedAsync(Guid ticketId, CancellationToken ct = default);
}

/// <summary>The rule tester's read-only answer: what one rule would do against a real ticket, without acting on it.</summary>
public record AssignmentPreview(bool ConditionsMatched, Guid? ChosenAgentId, int CandidateCount, int EligibleCount, string Reason);

/// <summary>Evaluates assignment rules and picks the agent for a ticket.</summary>
public interface IAssignmentEngine
{
    /// <summary>Returns the chosen agent, or null when the ticket should stay in a team queue.</summary>
    Task<Guid?> AssignAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Evaluates ONE rule against an existing ticket and reports what would happen — the rule
    /// tester. Never mutates the ticket, the rule's match statistics, or the decision log.
    /// </summary>
    Task<AssignmentPreview> PreviewRuleAsync(Guid ticketId, Guid ruleId, CancellationToken ct = default);
}

/// <summary>
/// Delivers one already-composed notification through an external channel (email, SMS or push).
/// Until CS-301/CS-302/CS-304 (the real channel providers) land, the registered implementation is a
/// logging placeholder — the outbox dispatcher job (CS-504) is built against this interface so
/// swapping in a real sender later is a DI registration change, not a call-site change.
/// </summary>
public interface IExternalNotificationSender
{
    Task SendAsync(
        NotificationChannel channel,
        string? recipientEmail,
        string? recipientPhone,
        string title,
        string body,
        string? link,
        CancellationToken ct = default);
}

/// <summary>The shape pushed to a connected client the instant a notification row commits.</summary>
public record RealtimeNotification(
    Guid Id, string EventType, string TitleEn, string TitleAr, string BodyEn, string BodyAr,
    string? Link, string Severity, DateTimeOffset CreatedAt);

/// <summary>
/// Pushes a notification to a signed-in user's connected clients over SignalR. Implemented in the
/// API project, where the hub type lives — Infrastructure depends only on this interface, the same
/// inversion <see cref="ICurrentUser"/> already uses, so the real-time push interceptor never
/// references SignalR or hub types directly.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyAsync(Guid userId, RealtimeNotification notification, CancellationToken ct = default);
}

/// <summary>
/// One kind of outbox row the generic dispatcher (<c>OutboxDispatcherJob</c>) knows how to deliver.
/// Registered as a collection — the job claims a due row, finds the handler whose
/// <see cref="CanHandle"/> matches its <c>Type</c> prefix, and calls <see cref="HandleAsync"/>.
/// A row whose type matches no handler is abandoned immediately with a clear error rather than
/// retried forever. Introduced by CS-301 (email) so CS-302/304's WhatsApp/SMS sends and CS-504's
/// notification fan-out share one claim/backoff/abandon loop instead of one job each.
/// </summary>
public interface IOutboxMessageHandler
{
    bool CanHandle(string type);

    /// <summary>Delivers one row. Must not call <c>SaveChangesAsync</c> — the job persists whatever this adds/changes together with the row's own status.</summary>
    Task HandleAsync(Domain.Integrations.OutboxMessage message, CancellationToken ct);

    /// <summary>
    /// Optional whole-table pass run once per tick before any row is claimed (for example CS-504's
    /// digest collapsing). Most handlers have nothing to do here.
    /// </summary>
    Task CollapseAsync(DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
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
/// Source of the auto-close window (<c>tickets.autoCloseResolvedAfterDays</c>). Reads plain
/// configuration today for the same reason <see cref="IAttachmentPolicyProvider"/> does — CS-1004
/// (system configuration) is not built yet. Async so a database-backed implementation is a config
/// change later, not a signature change.
/// </summary>
public interface IAutoCloseSettingsProvider
{
    Task<double> GetAutoCloseResolvedAfterDaysAsync(CancellationToken ct = default);
}

/// <summary>
/// Source of the audit retention window (<c>audit.retentionDays</c>), read by the nightly retention
/// job. Same shape and reasoning as <see cref="IAutoCloseSettingsProvider"/>: async so that backing
/// it with a <c>SystemSetting</c> row once CS-1004 lands is a configuration change, not a signature
/// change. Zero means keep forever.
/// </summary>
public interface IAuditRetentionSettings
{
    Task<int> GetRetentionDaysAsync(CancellationToken ct = default);
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
