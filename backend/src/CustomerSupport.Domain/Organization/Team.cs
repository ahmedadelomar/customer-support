using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Organization;

/// <summary>A group of agents inside a department; the unit automatic assignment distributes work across.</summary>
public class Team : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public LocalizedText Name { get; set; } = new();
    public Guid? LeadUserId { get; set; }
    /// <summary>Cursor used by the round-robin assignment strategy; advanced under a row lock.</summary>
    public int RoundRobinCursor { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
