using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;

namespace CustomerSupport.Application.Branding;

/// <summary>
/// Stores a logo through <see cref="IFileStorage"/> and returns its URL
/// (Platform / Runtime branding and theming).
/// </summary>
/// <remarks>
/// SVG is refused rather than sanitised. An SVG is a document that can carry <c>&lt;script&gt;</c> and
/// event handlers, and it is served from our own origin — so a malicious one is stored XSS against
/// every visitor. Stripping scripts reliably is a much harder problem than it looks, and raster
/// formats cost a logo nothing.
/// </remarks>
[RequirePermission(Permissions.Administration.ManageBranding)]
public record UploadBrandingLogoCommand(
    Stream Content, string FileName, string ContentType, long Length) : IRequest<string>;

public class UploadBrandingLogoCommandHandler(IFileStorage storage)
    : IRequestHandler<UploadBrandingLogoCommand, string>
{
    private const long MaxBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp",
    };

    public async Task<string> Handle(UploadBrandingLogoCommand request, CancellationToken cancellationToken)
    {
        if (request.Length > MaxBytes)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["The logo must be 2MB or smaller."],
            });
        }

        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["The logo must be a PNG, JPEG or WebP image. SVG is not accepted."],
            });
        }

        return await storage.SaveAsync(request.Content, request.FileName, request.ContentType, cancellationToken);
    }
}
