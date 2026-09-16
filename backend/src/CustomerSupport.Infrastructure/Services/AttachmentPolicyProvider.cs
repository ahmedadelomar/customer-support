using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads the attachment size cap and extension allow-list from the settings table, so both are
/// editable at runtime (Security &amp; Administration / System configuration) rather than needing a
/// redeploy. Falls back to the compiled-in defaults when no row exists.
/// </summary>
public class AttachmentPolicyProvider(ISettingsProvider settings) : IAttachmentPolicyProvider
{
    public async Task<AttachmentPolicy> GetPolicyAsync(CancellationToken ct = default)
    {
        var maxBytesDefault = long.Parse(SettingKeys.Find(SettingKeys.AttachmentsMaxBytes)!.DefaultValue);
        var extensionsDefault = SettingKeys.Find(SettingKeys.AttachmentsAllowedExtensions)!.DefaultValue;

        var maxBytes = await settings.GetAsync(SettingKeys.AttachmentsMaxBytes, maxBytesDefault, ct: ct);
        var configured = await settings.GetAsync(SettingKeys.AttachmentsAllowedExtensions, extensionsDefault, ct: ct);

        // Stored without leading dots for readability on the settings screen; normalised here so the
        // comparison against Path.GetExtension() still matches.
        var extensions = configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
            .ToHashSet();

        return new AttachmentPolicy(maxBytes, extensions);
    }
}
