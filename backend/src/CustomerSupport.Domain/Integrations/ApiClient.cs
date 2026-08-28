using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Integrations;

/// <summary>
/// A machine consumer of the public API (Integrations / APIs). Authenticates with the client
/// credentials flow; only the secret hash is stored, so a lost secret must be rotated, not recovered.
/// </summary>
public class ApiClient : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecretHash { get; set; } = string.Empty;
    /// <summary>Last four characters of the secret, shown so an admin can identify a key.</summary>
    public string? SecretHint { get; set; }

    /// <summary>Space-separated permission keys this client may exercise, a subset of the role model.</summary>
    public string Scopes { get; set; } = string.Empty;
    /// <summary>Optional CIDR allow-list. Empty means any source address.</summary>
    public string? AllowedIpRanges { get; set; }
    public int RateLimitPerMinute { get; set; } = 120;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? SecretRotatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
