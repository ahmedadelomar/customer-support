using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Organization;

/// <summary>
/// Per-branch white-label theme (Platform / Custom branding). A row with a null
/// <see cref="ITenantScoped.BranchId"/> is the system-wide default.
/// </summary>
public class BrandingSetting : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public LocalizedText ProductName { get; set; } = new();
    public string? LogoUrl { get; set; }
    public string? LogoDarkUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string PrimaryColor { get; set; } = "#5B2C8D";
    public string SecondaryColor { get; set; } = "#0E7490";
    public string? AccentColor { get; set; }
    public string? EmailHeaderHtml { get; set; }
    public string? EmailFooterHtml { get; set; }
    public string? PortalCustomCss { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
