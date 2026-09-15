using CustomerSupport.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>Reads the auto-close window from configuration. See the remark on <see cref="IAutoCloseSettingsProvider"/> for why this isn't backed by <c>SystemSetting</c> yet.</summary>
public class AutoCloseSettingsProvider(IConfiguration configuration) : IAutoCloseSettingsProvider
{
    public Task<double> GetAutoCloseResolvedAfterDaysAsync(CancellationToken ct = default) =>
        Task.FromResult(configuration.GetValue("Tickets:AutoCloseResolvedAfterDays", 3.0));
}
