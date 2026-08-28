using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Integrations;

/// <summary>
/// A configured outbound connection to an external system: ERP, email, SMS or WhatsApp provider.
/// Credentials live in <see cref="CredentialsEncrypted"/> and are never returned by the API.
/// </summary>
public class IntegrationConnection : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public IntegrationType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Adapter key selecting the implementation, for example <c>smtp</c> or <c>whatsapp-cloud</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    public string? BaseUrl { get; set; }
    /// <summary>Non-secret settings as JSON, safe to return to admins.</summary>
    public string SettingsJson { get; set; } = "{}";
    /// <summary>Secrets protected with the data-protection key ring. Write-only through the API.</summary>
    public string? CredentialsEncrypted { get; set; }

    public IntegrationStatus Status { get; set; } = IntegrationStatus.NotConfigured;
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastHealthCheckAt { get; set; }
    public string? LastHealthCheckError { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    /// <summary>Consecutive failures. Used to open a circuit breaker and mark the connection Degraded.</summary>
    public int ConsecutiveFailureCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
