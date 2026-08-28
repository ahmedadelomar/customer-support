using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Reporting;

/// <summary>
/// A named grid of widgets (Reports and Management / Management dashboards). Scope decides who sees it.
/// </summary>
public class Dashboard : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public LocalizedText Name { get; set; } = new();
    /// <summary>Personal, Role or Global.</summary>
    public string Scope { get; set; } = "Personal";
    public Guid? OwnerId { get; set; }
    /// <summary>Set for role-scoped dashboards, for example a supervisor overview.</summary>
    public Guid? RoleId { get; set; }

    /// <summary>Landing dashboard for its scope when the user has no explicit preference.</summary>
    public bool IsDefault { get; set; }
    /// <summary>Auto-refresh interval in seconds; zero disables polling.</summary>
    public int RefreshIntervalSeconds { get; set; } = 60;

    public ICollection<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
