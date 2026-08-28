using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Identity;

/// <summary>
/// A single-use refresh token. Only the hash is stored, so a leaked database row cannot be replayed
/// against the API, and rotation makes a stolen token useless after the next legitimate refresh.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the issued token. The raw value is returned once and never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByIp { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    /// <summary>
    /// Set when this token was rotated, pointing at its successor. Presenting an already-rotated
    /// token means the value leaked, which is why the whole chain is revoked when it happens.
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
