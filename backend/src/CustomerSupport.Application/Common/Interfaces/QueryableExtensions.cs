using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// Query helpers shared by handlers. Branch scoping lives here rather than in a global query filter
/// so that background jobs and cross-branch reports can deliberately opt out.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Restricts a tenant-scoped query to the branches the caller may see. Rows with a null
    /// <c>BranchId</c> are global and always visible. A user with no branch restrictions
    /// (a system administrator) sees everything.
    /// </summary>
    public static IQueryable<T> WhereBranchAccessible<T>(this IQueryable<T> query, ICurrentUser user)
        where T : class, ITenantScoped
    {
        if (user.AccessibleBranchIds.Count == 0)
        {
            return query;
        }

        var allowed = user.AccessibleBranchIds.ToList();
        return query.Where(e => e.BranchId == null || allowed.Contains(e.BranchId.Value));
    }
}
