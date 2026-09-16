using CustomerSupport.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads the audit retention window from configuration, defaulting to 400 days. See the remark on
/// <see cref="IAuditRetentionSettings"/> for why this is not backed by <c>SystemSetting</c> yet.
/// </summary>
public class AuditRetentionSettings(IConfiguration configuration) : IAuditRetentionSettings
{
    public Task<int> GetRetentionDaysAsync(CancellationToken ct = default) =>
        Task.FromResult(configuration.GetValue("Audit:RetentionDays", 400));
}
