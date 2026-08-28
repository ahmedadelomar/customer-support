using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Integrations;

/// <summary>
/// An outbound HTTP subscription letting external systems react to CRM events
/// (Integrations / External systems). Payloads are signed with <see cref="Secret"/>.
/// </summary>
public class Webhook : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>Shared secret used for the HMAC-SHA256 signature header.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Comma-separated event keys, for example <c>ticket.created,ticket.resolved</c>.</summary>
    public string Events { get; set; } = string.Empty;
    /// <summary>Extra static headers as JSON, for a gateway token for instance.</summary>
    public string? HeadersJson { get; set; }

    public bool IsActive { get; set; } = true;
    public int MaxRetries { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Consecutive failures. The webhook auto-disables once it exceeds the retry budget.</summary>
    public int ConsecutiveFailureCount { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
