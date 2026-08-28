using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Integrations;

/// <summary>One sync run against an external system, with enough detail to diagnose partial failures.</summary>
public class IntegrationSyncLog : BaseEntity
{
    public Guid ConnectionId { get; set; }
    public IntegrationConnection Connection { get; set; } = null!;

    public SyncDirection Direction { get; set; }
    /// <summary>Entity synced, for example Customer or Invoice.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Running, Succeeded, PartiallyFailed or Failed.</summary>
    public string Status { get; set; } = "Running";
    public int RecordsRead { get; set; }
    public int RecordsWritten { get; set; }
    public int RecordsSkipped { get; set; }
    public int RecordsFailed { get; set; }

    /// <summary>Cursor or watermark to resume from on the next incremental run.</summary>
    public string? Watermark { get; set; }
    public string? Error { get; set; }
    /// <summary>Correlates this run with application logs.</summary>
    public string? CorrelationId { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
