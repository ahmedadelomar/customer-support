using System.Globalization;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads and writes <see cref="SystemSetting"/> rows with a short-lived cache
/// (Security &amp; Administration / System configuration).
/// </summary>
/// <remarks>
/// Cached per branch and key with a 10-minute sliding expiry and explicit invalidation on write, so
/// a settings change takes effect on the next request rather than at the next deployment. Secrets go
/// through DataProtection: encrypted at rest, decrypted only here.
/// </remarks>
public class SettingsProvider(
    AppDbContext db,
    IMemoryCache cache,
    IDataProtectionProvider dataProtection,
    IDateTimeProvider clock,
    ILogger<SettingsProvider> logger) : ISettingsProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("CustomerSupport.SystemSettings");

    public async Task<T> GetAsync<T>(
        string key, T defaultValue, Guid? branchId = null, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(key, branchId);

        if (cache.TryGetValue(cacheKey, out string? cached))
        {
            return Convert<T>(cached, defaultValue, key);
        }

        var raw = await ResolveRawAsync(key, branchId, ct);
        cache.Set(cacheKey, raw, new MemoryCacheEntryOptions { SlidingExpiration = CacheLifetime });

        return Convert<T>(raw, defaultValue, key);
    }

    public async Task SetAsync(string key, string? value, Guid? branchId, CancellationToken ct = default)
    {
        var definition = SettingKeys.Find(key);

        var row = await db.Set<SystemSetting>()
            .FirstOrDefaultAsync(s => s.Key == key && s.BranchId == branchId, ct);

        var stored = definition?.IsSecret == true && !string.IsNullOrEmpty(value)
            ? _protector.Protect(value)
            : value;

        if (row is null)
        {
            db.Set<SystemSetting>().Add(new SystemSetting
            {
                Key = key,
                Value = stored,
                BranchId = branchId,
                DataType = definition?.DataType ?? "string",
                Category = definition?.Category ?? "General",
                Name = definition is null
                    ? new LocalizedText(key, key)
                    : new LocalizedText(definition.NameEn, definition.NameAr),
                Description = definition?.DescriptionEn,
                IsSecret = definition?.IsSecret ?? false,

                // A branch override is operational data, not part of the shipped configuration set,
                // so it stays deletable while the global row it overrides does not.
                IsSystem = definition is not null && branchId is null,
                CreatedAt = clock.UtcNow,
            });
        }
        else
        {
            row.Value = stored;
        }

        await db.SaveChangesAsync(ct);
        Invalidate(key);
    }

    public void Invalidate(string key)
    {
        // Branch overrides are cached under their own keys, so clear the global entry and every
        // branch entry that exists. Cheap: the branch list is small and read from the tracked set.
        cache.Remove(CacheKey(key, null));

        foreach (var branchId in db.Branches.Select(b => b.Id).ToList())
        {
            cache.Remove(CacheKey(key, branchId));
        }
    }

    /// <summary>Branch row, then global row, then the compiled-in default. Secrets are decrypted here.</summary>
    private async Task<string?> ResolveRawAsync(string key, Guid? branchId, CancellationToken ct)
    {
        var rows = await db.Set<SystemSetting>().AsNoTracking()
            .Where(s => s.Key == key && (s.BranchId == null || s.BranchId == branchId))
            .ToListAsync(ct);

        var row = rows.FirstOrDefault(s => branchId is not null && s.BranchId == branchId)
                  ?? rows.FirstOrDefault(s => s.BranchId == null);

        var definition = SettingKeys.Find(key);

        if (row?.Value is null)
        {
            return definition?.DefaultValue;
        }

        if (!row.IsSecret)
        {
            return row.Value;
        }

        try
        {
            return _protector.Unprotect(row.Value);
        }
        catch (Exception ex)
        {
            // A value encrypted with keys that no longer exist must not take the whole app down;
            // fall back to the default and make the cause findable.
            logger.LogError(ex, "Could not decrypt the secret setting {Key}; using its default.", key);
            return definition?.DefaultValue;
        }
    }

    private static string CacheKey(string key, Guid? branchId) =>
        $"settings:{branchId?.ToString() ?? "global"}:{key}";

    private static T Convert<T>(string? raw, T defaultValue, string key)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            if (target == typeof(string)) return (T)(object)raw;
            if (target == typeof(int)) return (T)(object)int.Parse(raw, CultureInfo.InvariantCulture);
            if (target == typeof(long)) return (T)(object)long.Parse(raw, CultureInfo.InvariantCulture);
            if (target == typeof(double)) return (T)(object)double.Parse(raw, CultureInfo.InvariantCulture);
            if (target == typeof(bool)) return (T)(object)bool.Parse(raw);
            if (target == typeof(Guid)) return (T)(object)Guid.Parse(raw);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // Values are validated on write, so this only happens to rows edited straight in the
            // database. The default keeps the feature working rather than failing the request.
            return defaultValue;
        }

        throw new NotSupportedException($"Setting '{key}' cannot be converted to {typeof(T).Name}.");
    }
}
