using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Tickets;

/// <summary>Free-form label attachable to tickets for ad-hoc grouping and reporting.</summary>
public class Tag : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#64748B";
    public int UsageCount { get; set; }
}
