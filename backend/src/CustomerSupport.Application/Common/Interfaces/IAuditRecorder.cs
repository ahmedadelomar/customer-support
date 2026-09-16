using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// Writes audit entries for things the <c>AuditLogInterceptor</c> cannot see: it only observes entity
/// mutations, so sign-in, sign-out, permission changes and exports would otherwise leave no trace
/// (Security &amp; Administration / Audit logs).
/// </summary>
public interface IAuditRecorder
{
    /// <summary>
    /// Appends one entry and saves it immediately — unlike the interceptor, these events are not part
    /// of any surrounding unit of work, and a failed sign-in has no other write to ride along with.
    /// </summary>
    /// <remarks>
    /// <c>userName</c> exists for the cases where <see cref="ICurrentUser"/> cannot supply it: a failed
    /// sign-in records the attempted username, since no one is authenticated at that point.
    /// </remarks>
    Task RecordAsync(
        AuditAction action,
        string entityType,
        string? entityId = null,
        object? metadata = null,
        string? userName = null,
        CancellationToken ct = default);
}
