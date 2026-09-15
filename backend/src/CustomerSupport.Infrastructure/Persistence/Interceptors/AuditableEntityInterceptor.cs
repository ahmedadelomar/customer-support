using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CustomerSupport.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit columns and converts deletes of <see cref="ISoftDeletable"/> entities into updates.
/// Centralising this means no handler can forget to set CreatedBy or accidentally hard-delete.
/// </summary>
public class AuditableEntityInterceptor(ICurrentUser currentUser, IDateTimeProvider clock)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = now;
                        auditable.CreatedById = userId;
                        break;
                    case EntityState.Modified:
                        auditable.ModifiedAt = now;
                        auditable.ModifiedById = userId;
                        break;
                }
            }

            // Owned-type edits leave the parent Unchanged, which would lose the ModifiedAt stamp.
            if (entry.State is EntityState.Added or EntityState.Modified &&
                entry.Entity is not IAuditable &&
                entry.Metadata.IsOwned() &&
                FindOwner(entry) is IAuditable owner)
            {
                owner.ModifiedAt = now;
                owner.ModifiedById = userId;
            }

            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable deletable })
            {
                entry.State = EntityState.Modified;
                deletable.IsDeleted = true;
                deletable.DeletedAt = now;
                deletable.DeletedById = userId;

                // EF Core had already cascaded this entity's owned-type dependents (e.g. a
                // LocalizedText Name/Body sharing the same table row) to EntityState.Deleted before
                // this interceptor ever ran. Converting the OWNER back to Modified does not un-cascade
                // them: left as Deleted, EF still nulls out their columns as part of the same shared
                // UPDATE, which trips a NOT NULL constraint on the first soft-deletable entity that
                // also owns a required LocalizedText (first hit: deleting a QuickReply). Walk the
                // owner's own reference navigations forward to find those dependents and reset them —
                // more reliable than the dependent's back-reference, which the cascade may have
                // already severed by this point.
                foreach (var navigation in entry.Navigations)
                {
                    if (navigation is ReferenceEntry { TargetEntry.State: EntityState.Deleted } reference)
                    {
                        reference.TargetEntry!.State = EntityState.Modified;
                    }
                }
            }
        }
    }

    /// <summary>
    /// For an owned-type entry (such as a <c>LocalizedText</c>), returns the entity that owns it.
    /// </summary>
    /// <remarks>
    /// The owner is reached through the navigation that points from the dependent back to the
    /// principal. <c>IsOnDependent</c> is declared on <see cref="INavigation"/> rather than on
    /// <c>INavigationBase</c> (which also covers skip navigations), so the pattern match narrows
    /// to a reference navigation before testing it.
    /// </remarks>
    private static object? FindOwner(EntityEntry entry) =>
        entry.Navigations
            .FirstOrDefault(n => n.Metadata is INavigation { IsOnDependent: true })?
            .CurrentValue;
}
