using CustomerSupport.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads the attachment size cap and extension allow-list from configuration. See the remark on
/// <see cref="IAttachmentPolicyProvider"/> for why this isn't backed by <c>SystemSetting</c> yet.
/// </summary>
public class AttachmentPolicyProvider(IConfiguration configuration) : IAttachmentPolicyProvider
{
    private static readonly string[] DefaultExtensions =
        [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv"];

    public Task<AttachmentPolicy> GetPolicyAsync(CancellationToken ct = default)
    {
        var maxBytes = configuration.GetValue("Attachments:MaxBytes", 10 * 1024 * 1024L);

        var configured = configuration.GetSection("Attachments:AllowedExtensions").Get<string[]>();
        var extensions = (configured is { Length: > 0 } ? configured : DefaultExtensions)
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
            .ToHashSet();

        return Task.FromResult(new AttachmentPolicy(maxBytes, extensions));
    }
}
