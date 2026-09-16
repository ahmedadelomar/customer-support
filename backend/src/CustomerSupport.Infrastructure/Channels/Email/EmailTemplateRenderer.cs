using System.Net;
using CustomerSupport.Application.Branding;
using CustomerSupport.Application.Channels.Outbound;
using MediatR;

namespace CustomerSupport.Infrastructure.Channels.Email;

/// <summary>
/// Wraps a reply in the tenant's branding (Platform / Custom branding) by resolving the same
/// <c>GetBrandingQuery</c> the portal and admin screens use — one resolution order (branch, then
/// global, then the shipped defaults), never a second copy of it here.
/// </summary>
public class EmailTemplateRenderer(ISender sender) : IEmailTemplateRenderer
{
    public async Task<string> RenderAsync(
        string bodyHtml, string signatureHtml, string language, Guid? branchId, CancellationToken ct = default)
    {
        var branding = await sender.Send(new GetBrandingQuery(branchId), ct);
        var isRtl = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var productName = isRtl ? branding.ProductNameAr : branding.ProductNameEn;

        var header = branding.EmailHeaderHtml ?? $"""
            <div style="background:{branding.PrimaryColor};padding:16px 20px;color:#ffffff;font-family:Arial,sans-serif;font-size:16px;font-weight:600;">{WebUtility.HtmlEncode(productName)}</div>
            """;

        var footerText = branding.SupportEmail is null
            ? productName
            : $"{productName} · {branding.SupportEmail}";
        var footer = branding.EmailFooterHtml ?? $"""
            <div style="padding:16px 20px;color:#64748b;font-family:Arial,sans-serif;font-size:12px;">{WebUtility.HtmlEncode(footerText)}</div>
            """;

        var signatureBlock = string.IsNullOrWhiteSpace(signatureHtml)
            ? ""
            : $"""<div style="padding:0 20px 16px;color:#475569;font-family:Arial,sans-serif;font-size:13px;">{signatureHtml}</div>""";

        return $"""
            <!doctype html>
            <html dir="{(isRtl ? "rtl" : "ltr")}" lang="{language}">
            <body style="margin:0;background:#f1f5f9;">
            <div style="max-width:600px;margin:0 auto;background:#ffffff;">
            {header}
            <div style="padding:20px;color:#1e293b;font-family:Arial,sans-serif;font-size:14px;line-height:1.6;">{bodyHtml}</div>
            {signatureBlock}
            {footer}
            </div>
            </body>
            </html>
            """;
    }
}
