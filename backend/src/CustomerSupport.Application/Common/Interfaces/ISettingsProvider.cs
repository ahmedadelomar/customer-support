namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>Where a resolved setting value came from, so the screen can show inheritance honestly.</summary>
public enum SettingSource
{
    /// <summary>No row exists; the compiled-in default from <c>SettingKeys</c> was used.</summary>
    Default = 0,

    /// <summary>A row with a null <c>BranchId</c>.</summary>
    Global = 1,

    /// <summary>A row scoped to the caller's branch, overriding the global one.</summary>
    Branch = 2,
}

/// <summary>
/// Typed, cached access to runtime configuration (Security &amp; Administration / System configuration).
/// </summary>
/// <remarks>
/// Resolution order is branch row, then global row, then the compiled-in default — the same order
/// CS-1204 reuses for every branch-scoped override. A missing setting returns its default, never
/// null, so callers never null-check configuration. Secrets are decrypted on read and encrypted on
/// write; they are never returned by the list projection.
/// </remarks>
public interface ISettingsProvider
{
    Task<T> GetAsync<T>(string key, T defaultValue, Guid? branchId = null, CancellationToken ct = default);

    /// <summary>Writes the value and invalidates the cache, so no restart is needed.</summary>
    Task SetAsync(string key, string? value, Guid? branchId, CancellationToken ct = default);

    void Invalidate(string key);
}
