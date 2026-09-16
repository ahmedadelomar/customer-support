using System.Text.RegularExpressions;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Branding;

/// <summary>Writes the theme for a branch, or the global default when no branch is given.</summary>
[RequirePermission(Permissions.Administration.ManageBranding)]
public record UpdateBrandingCommand : IRequest
{
    public Guid? BranchId { get; init; }
    public string ProductNameEn { get; init; } = string.Empty;
    public string ProductNameAr { get; init; } = string.Empty;
    public string PrimaryColor { get; init; } = BrandingDefaults.PrimaryColor;
    public string SecondaryColor { get; init; } = BrandingDefaults.SecondaryColor;
    public string? AccentColor { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string? EmailHeaderHtml { get; init; }
    public string? EmailFooterHtml { get; init; }
    public string? PortalCustomCss { get; init; }
    public string? SupportEmail { get; init; }
    public string? SupportPhone { get; init; }
}

public class UpdateBrandingCommandValidator : AbstractValidator<UpdateBrandingCommand>
{
    private const string ColourMessage = "Colour must be a 6-digit hex value such as #5B2C8D.";

    public UpdateBrandingCommandValidator()
    {
        RuleFor(x => x.ProductNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProductNameAr).NotEmpty().MaximumLength(200);

        // These values end up in a `style.setProperty` call on the client. An unvalidated string
        // there is a CSS injection vector, so the format is enforced before it is ever stored.
        RuleFor(x => x.PrimaryColor).Matches(BrandingDefaults.HexPattern).WithMessage(ColourMessage);
        RuleFor(x => x.SecondaryColor).Matches(BrandingDefaults.HexPattern).WithMessage(ColourMessage);
        RuleFor(x => x.AccentColor).Matches(BrandingDefaults.HexPattern).WithMessage(ColourMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.AccentColor));

        RuleFor(x => x.SupportEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SupportEmail));
    }
}

public class UpdateBrandingCommandHandler(IAppDbContext db) : IRequestHandler<UpdateBrandingCommand>
{
    public async Task Handle(UpdateBrandingCommand request, CancellationToken cancellationToken)
    {
        var row = await db.BrandingSettings
            .FirstOrDefaultAsync(b => b.BranchId == request.BranchId, cancellationToken);

        if (row is null)
        {
            row = new BrandingSetting { BranchId = request.BranchId };
            db.BrandingSettings.Add(row);
        }

        row.ProductName = new LocalizedText(request.ProductNameEn, request.ProductNameAr);
        row.PrimaryColor = request.PrimaryColor;
        row.SecondaryColor = request.SecondaryColor;
        row.AccentColor = request.AccentColor;
        row.LogoUrl = request.LogoUrl;
        row.FaviconUrl = request.FaviconUrl;
        row.EmailHeaderHtml = request.EmailHeaderHtml;
        row.EmailFooterHtml = request.EmailFooterHtml;
        row.PortalCustomCss = PortalCss.Sanitize(request.PortalCustomCss);
        row.SupportEmail = request.SupportEmail;
        row.SupportPhone = request.SupportPhone;

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Removes a branch's override, reverting it to the global theme.</summary>
[RequirePermission(Permissions.Administration.ManageBranding)]
public record DeleteBrandingOverrideCommand(Guid BranchId) : IRequest;

public class DeleteBrandingOverrideCommandHandler(IAppDbContext db)
    : IRequestHandler<DeleteBrandingOverrideCommand>
{
    public async Task Handle(DeleteBrandingOverrideCommand request, CancellationToken cancellationToken)
    {
        var row = await db.BrandingSettings
            .FirstOrDefaultAsync(b => b.BranchId == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("BrandingOverride", request.BranchId);

        db.BrandingSettings.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Strips the parts of portal CSS that can reach outside a stylesheet.
/// </summary>
/// <remarks>
/// Custom CSS is an administrator-supplied string served to every portal visitor. `@import` pulls in
/// a third-party stylesheet, `expression(` executes script in old engines, `javascript:` URLs execute
/// on navigation, and a stray `&lt;/style` closes the block and escapes into markup. Everything left
/// is still scoped to the portal root by the caller, so it cannot reach the agent workspace.
/// </remarks>
public static partial class PortalCss
{
    public static string? Sanitize(string? css)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            return css;
        }

        var cleaned = DangerousConstructs().Replace(css, string.Empty);
        return cleaned.Trim();
    }

    [GeneratedRegex(@"@import|expression\s*\(|javascript\s*:|</\s*style|<\s*script",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex DangerousConstructs();
}
