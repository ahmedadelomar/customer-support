using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Organization;

/// <summary>A physical or logical branch. Root of the multi-branch scoping model.</summary>
public class Branch : BaseEntity, IAuditable, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public string? TimeZoneId { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Department> Departments { get; set; } = new List<Department>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
