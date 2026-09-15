using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Tickets;

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

    /// <summary>
    /// Restricts a ticket query to what the caller may see: everything when they hold
    /// <c>tickets.view.all</c>, otherwise their own department plus anything assigned to them.
    /// Declared here (rather than duplicated by CS-1203) per that story's own instruction to
    /// implement the shared department-scoping helper wherever the <see cref="Ticket"/> entity lands.
    /// </summary>
    public static IQueryable<Ticket> WhereTicketVisible(this IQueryable<Ticket> query, ICurrentUser user)
    {
        if (user.HasPermission(Permissions.Tickets.ViewAll))
        {
            return query;
        }

        var departments = user.DepartmentIds.ToList();
        var userId = user.UserId;

        return query.Where(t =>
            t.AssignedAgentId == userId ||
            (t.DepartmentId != null && departments.Contains(t.DepartmentId.Value)));
    }
}
