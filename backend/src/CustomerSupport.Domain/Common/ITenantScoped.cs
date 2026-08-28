namespace CustomerSupport.Domain.Common;

/// <summary>
/// Multi-branch scoping (Platform / Multi-branch). A global query filter restricts reads to the
/// branches the current principal may access; <c>null</c> means the row is global to all branches.
/// </summary>
public interface ITenantScoped
{
    Guid? BranchId { get; set; }
}
