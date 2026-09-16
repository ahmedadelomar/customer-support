using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Common.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Settings.Queries;

/// <summary>The mask returned in place of any secret value. Resubmitting it is a no-op, not an overwrite.</summary>
public static class SecretMask
{
    public const string Value = "********";
}

/// <summary>One setting, resolved for the caller's scope.</summary>
public record SettingDto
{
    public string Key { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public string? Value { get; init; }
    public SettingSource Source { get; init; }
    public bool IsSecret { get; init; }
    public bool IsSystem { get; init; }
}

/// <summary>Settings grouped by category, as the screen renders them.</summary>
public record SettingCategoryDto
{
    public string Category { get; init; } = string.Empty;
    public IReadOnlyList<SettingDto> Settings { get; init; } = [];
}

/// <summary>
/// Every configurable setting with its resolved value and where that value came from
/// (Security &amp; Administration / System configuration).
/// </summary>
[RequirePermission(Permissions.Administration.ManageSettings)]
public record GetSettingsQuery(Guid? BranchId = null) : IRequest<IReadOnlyList<SettingCategoryDto>>;

public class GetSettingsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSettingsQuery, IReadOnlyList<SettingCategoryDto>>
{
    public async Task<IReadOnlyList<SettingCategoryDto>> Handle(
        GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var branchId = request.BranchId ?? currentUser.BranchId;

        var rows = await db.SystemSettings.AsNoTracking()
            .Where(s => s.BranchId == null || s.BranchId == branchId)
            .ToListAsync(cancellationToken);

        var settings = SettingKeys.All.Select(definition =>
        {
            var branchRow = branchId is null
                ? null
                : rows.FirstOrDefault(r => r.Key == definition.Key && r.BranchId == branchId);

            var globalRow = rows.FirstOrDefault(r => r.Key == definition.Key && r.BranchId == null);
            var row = branchRow ?? globalRow;

            var source = branchRow is not null ? SettingSource.Branch
                : globalRow is not null ? SettingSource.Global
                : SettingSource.Default;

            return new SettingDto
            {
                Key = definition.Key,
                Category = definition.Category,
                DataType = definition.DataType,
                NameEn = definition.NameEn,
                NameAr = definition.NameAr,
                DescriptionEn = definition.DescriptionEn,
                DescriptionAr = definition.DescriptionAr,

                // Masked in the projection, not in the controller, so no future caller of this query
                // can accidentally bypass it.
                Value = definition.IsSecret
                    ? (string.IsNullOrEmpty(row?.Value) ? null : SecretMask.Value)
                    : row?.Value ?? definition.DefaultValue,

                Source = source,
                IsSecret = definition.IsSecret,
                IsSystem = true,
            };
        });

        return settings
            .GroupBy(s => s.Category)
            .Select(g => new SettingCategoryDto { Category = g.Key, Settings = g.ToList() })
            .OrderBy(g => g.Category)
            .ToList();
    }
}

/// <summary>
/// The small allow-listed subset the portal needs before anyone signs in. Anything not named in
/// <see cref="SettingKeys.PublicKeys"/> is never returned here, whatever its category.
/// </summary>
public record GetPublicSettingsQuery : IRequest<IReadOnlyDictionary<string, string?>>;

public class GetPublicSettingsQueryHandler(ISettingsProvider settings)
    : IRequestHandler<GetPublicSettingsQuery, IReadOnlyDictionary<string, string?>>
{
    public async Task<IReadOnlyDictionary<string, string?>> Handle(
        GetPublicSettingsQuery request, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var key in SettingKeys.PublicKeys)
        {
            var definition = SettingKeys.Find(key);
            if (definition is null || definition.IsSecret)
            {
                continue;
            }

            result[key] = await settings.GetAsync<string>(
                key, definition.DefaultValue, ct: cancellationToken);
        }

        return result;
    }
}
