using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Integrations;

/// <summary>
/// Transactional outbox row. Domain events are written here in the same transaction as the state
/// change, then dispatched by a background worker. This is what makes notifications, webhooks and
/// outbound messages reliable rather than best-effort.
/// </summary>
public class OutboxMessage : BaseEntity
{
    /// <summary>Fully qualified event type name used to resolve the handler.</summary>
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Aggregate the event came from, for tracing and ordered processing.</summary>
    public string? AggregateType { get; set; }
    public Guid? AggregateId { get; set; }
    public string? CorrelationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    /// <summary>Backoff gate. The dispatcher only picks up rows whose time has come.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }
}
