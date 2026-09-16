using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads the audit retention window from the settings table
/// (<c>audit.retentionDays</c>), falling back to the compiled-in default when no row exists.
/// </summary>
public class AuditRetentionSettings(ISettingsProvider settings) : IAuditRetentionSettings
{
    public Task<int> GetRetentionDaysAsync(CancellationToken ct = default) =>
        settings.GetAsync(
            SettingKeys.AuditRetentionDays,
            int.Parse(SettingKeys.Find(SettingKeys.AuditRetentionDays)!.DefaultValue),
            ct: ct);
}
