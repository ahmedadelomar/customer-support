using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads the auto-close window from the settings table (<c>tickets.autoCloseResolvedAfterDays</c>),
/// falling back to the compiled-in default when no row exists.
/// </summary>
public class AutoCloseSettingsProvider(ISettingsProvider settings) : IAutoCloseSettingsProvider
{
    public async Task<double> GetAutoCloseResolvedAfterDaysAsync(CancellationToken ct = default) =>
        await settings.GetAsync(
            SettingKeys.TicketsAutoCloseResolvedAfterDays,
            double.Parse(SettingKeys.Find(SettingKeys.TicketsAutoCloseResolvedAfterDays)!.DefaultValue),
            ct: ct);
}
