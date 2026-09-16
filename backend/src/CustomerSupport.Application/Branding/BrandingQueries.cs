using CustomerSupport.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Branding;

/// <summary>The resolved theme for one branch, or the global default.</summary>
public record BrandingDto
{
    public Guid? BranchId { get; init; }
    public string ProductNameEn { get; init; } = "Customer Support CRM";
    public string ProductNameAr { get; init; } = "نظام دعم العملاء";
    public string? LogoUrl { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string PrimaryColor { get; init; } = BrandingDefaults.PrimaryColor;
    public string SecondaryColor { get; init; } = BrandingDefaults.SecondaryColor;
    public string? AccentColor { get; init; }
    public string? EmailHeaderHtml { get; init; }
    public string? EmailFooterHtml { get; init; }
    public string? PortalCustomCss { get; init; }
    public string? SupportEmail { get; init; }
    public string? SupportPhone { get; init; }

    /// <summary>True when this came from a branch row rather than the global one or the defaults.</summary>
    public bool IsBranchOverride { get; init; }
}

/// <summary>The shipped theme, used when no row exists at all.</summary>
public static class BrandingDefaults
{
    public const string PrimaryColor = "#5B2C8D";
    public const string SecondaryColor = "#0E7490";

    /// <summary>A colour must match this before it is ever written into a stylesheet.</summary>
    public const string HexPattern = "^#[0-9A-Fa-f]{6}$";
}

/// <summary>
/// Resolved branding for a branch (Platform / Runtime branding and theming).
/// </summary>
/// <remarks>
/// Deliberately carries no <c>[RequirePermission]</c> and is exposed anonymously: the portal needs the
/// logo, product name and colours before anyone signs in, and a theme is not sensitive. Writes are
/// still gated on <c>admin.branding.manage</c>.
/// </remarks>
public record GetBrandingQuery(Guid? BranchId = null) : IRequest<BrandingDto>;

public class GetBrandingQueryHandler(IAppDbContext db) : IRequestHandler<GetBrandingQuery, BrandingDto>
{
    public async Task<BrandingDto> Handle(GetBrandingQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.BrandingSettings.AsNoTracking()
            .Where(b => b.BranchId == null || b.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);

        // Branch row, then global row, then the shipped defaults — the same order every other
        // branch-scoped resolution in the product uses.
        var branchRow = request.BranchId is null
            ? null
            : rows.FirstOrDefault(r => r.BranchId == request.BranchId);

        var row = branchRow ?? rows.FirstOrDefault(r => r.BranchId == null);

        if (row is null)
        {
            return new BrandingDto();
        }

        return new BrandingDto
        {
            BranchId = row.BranchId,
            ProductNameEn = string.IsNullOrWhiteSpace(row.ProductName.En) ? "Customer Support CRM" : row.ProductName.En,
            ProductNameAr = string.IsNullOrWhiteSpace(row.ProductName.Ar) ? "نظام دعم العملاء" : row.ProductName.Ar,
            LogoUrl = row.LogoUrl,
            LogoDarkUrl = row.LogoDarkUrl,
            FaviconUrl = row.FaviconUrl,
            PrimaryColor = row.PrimaryColor,
            SecondaryColor = row.SecondaryColor,
            AccentColor = row.AccentColor,
            EmailHeaderHtml = row.EmailHeaderHtml,
            EmailFooterHtml = row.EmailFooterHtml,
            PortalCustomCss = row.PortalCustomCss,
            SupportEmail = row.SupportEmail,
            SupportPhone = row.SupportPhone,
            IsBranchOverride = branchRow is not null,
        };
    }
}
