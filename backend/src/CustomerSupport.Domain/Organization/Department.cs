using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Organization;

/// <summary>A support department (e.g. Billing, Technical). Tickets route to a department before a team/agent.</summary>
public class Department : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Code { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public string? Description { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? DefaultSlaPolicyId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Team> Teams { get; set; } = new List<Team>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
