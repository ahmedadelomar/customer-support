namespace CustomerSupport.Application.Tickets.Categories;

/// <summary>
/// One category row for the admin tree editor — unlike <c>TicketCategoryLookupDto</c> (used by the
/// create form and list filters), this includes inactive rows and the admin-only fields.
/// </summary>
public record TicketCategoryAdminDto
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Path { get; init; } = "/";
    public int Depth { get; init; }
    public int DisplayOrder { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public Guid? DefaultDepartmentId { get; init; }
    public Guid? DefaultSlaPolicyId { get; init; }
    public bool IsVisibleInPortal { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Tickets currently filed under this exact category — shown so an administrator can judge a deactivation.</summary>
    public int TicketCount { get; init; }
}
