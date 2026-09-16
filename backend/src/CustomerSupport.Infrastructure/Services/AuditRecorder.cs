using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Persistence.Interceptors;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Writes the explicit (non-entity) audit entries. Saves immediately: a failed sign-in has no
/// surrounding unit of work to ride along with, and losing the record of one would defeat the point.
/// </summary>
public class AuditRecorder(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditContextAccessor auditContext,
    IDateTimeProvider clock) : IAuditRecorder
{
    public async Task RecordAsync(
        AuditAction action,
        string entityType,
        string? entityId = null,
        object? metadata = null,
        string? userName = null,
        CancellationToken ct = default)
    {
        db.Set<AuditLog>().Add(new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = userName ?? currentUser.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,

            // Explicit events have no before/after state; whatever context matters travels as metadata.
            NewValues = metadata is null ? null : JsonSerializer.Serialize(metadata),
            IpAddress = auditContext.IpAddress,
            UserAgent = auditContext.UserAgent,
            CorrelationId = auditContext.CorrelationId,
            BranchId = currentUser.BranchId,
            OccurredAt = clock.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
