using CustomerSupport.Application.Common.Interfaces;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Placeholder <see cref="IVirusScanner"/> — no scanner is configured yet. Replace the DI
/// registration with a real implementation (e.g. a ClamAV client) when one is available; every
/// caller already handles the "infected" result, so that swap needs no other code change.
/// </summary>
public class NoOpVirusScanner : IVirusScanner
{
    public Task<string> ScanAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult("skipped");
}
