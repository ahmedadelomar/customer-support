using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CustomerSupport.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes the audit trail (Security and Administration / Audit logs). Runs before save so it can read
/// original values, and only records entity types opted in through <see cref="AuditedTypes"/> to keep
/// the trail useful rather than enormous.
/// </summary>
public class AuditLogInterceptor(
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IAuditContextAccessor auditContext) : SaveChangesInterceptor
{
    /// <summary>Entity names worth auditing: security, configuration and customer-facing records.</summary>
    private static readonly HashSet<string> AuditedTypes =
    [
        "ApplicationUser", "ApplicationRole", "RolePermission", "Permission",
        "Customer", "CustomerContact", "Ticket", "SystemSetting", "SlaPolicy", "SlaTarget",
        "AssignmentRule", "EscalationRule", "ApiClient", "IntegrationConnection", "Webhook",
        "Branch", "Department", "Team", "BrandingSetting", "KbArticle", "AiModelConfig",
    ];

    /// <summary>Never written to the trail, even when the entity is audited.</summary>
    private static readonly HashSet<string> RedactedProperties =
    [
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "ClientSecretHash",
        "CredentialsEncrypted", "Secret", "Token",
    ];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => AuditedTypes.Contains(e.Metadata.ClrType.Name))
            .ToList();

        if (entries.Count == 0) return;

        var logs = new List<AuditLog>(entries.Count);
        var now = clock.UtcNow;

        foreach (var entry in entries)
        {
            var (oldValues, newValues) = Diff(entry);

            // A Modified entry whose only changes were redacted or audit columns is not worth a row.
            if (entry.State == EntityState.Modified && newValues.Count == 0) continue;

            logs.Add(new AuditLog
            {
                UserId = currentUser.UserId,
                UserName = currentUser.UserName,
                BranchId = currentUser.BranchId,
                Action = ResolveAction(entry),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues),
                IpAddress = auditContext.IpAddress,
                UserAgent = auditContext.UserAgent,
                CorrelationId = auditContext.CorrelationId,
                OccurredAt = now,
            });
        }

        if (logs.Count != 0) context.Set<AuditLog>().AddRange(logs);
    }

    /// <summary>A soft delete arrives as Modified, so infer the intent from the IsDeleted flag.</summary>
    private static AuditAction ResolveAction(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditAction.Create,
        EntityState.Deleted => AuditAction.Delete,
        _ when entry.Entity is ISoftDeletable { IsDeleted: true } => AuditAction.Delete,
        _ => AuditAction.Update,
    };

    private static (Dictionary<string, object?> Old, Dictionary<string, object?> New) Diff(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (RedactedProperties.Contains(name)) continue;
            if (name is "CreatedAt" or "CreatedById" or "ModifiedAt" or "ModifiedById") continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = prop.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[name] = prop.OriginalValue;
                    break;
                case EntityState.Modified when prop.IsModified &&
                                               !Equals(prop.OriginalValue, prop.CurrentValue):
                    oldValues[name] = prop.OriginalValue;
                    newValues[name] = prop.CurrentValue;
                    break;
            }
        }

        return (oldValues, newValues);
    }
}

/// <summary>Request-scoped metadata the audit trail needs but the domain should not know about.</summary>
public interface IAuditContextAccessor
{
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
}
