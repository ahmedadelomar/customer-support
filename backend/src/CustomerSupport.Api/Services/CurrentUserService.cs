using System.Security.Claims;
using CustomerSupport.Application.Common.Interfaces;

namespace CustomerSupport.Api.Services;

/// <summary>
/// Reads the principal from the current HTTP request. Permissions are carried as <c>perm</c> claims
/// so authorisation needs no database round trip per request.
/// </summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Claim type holding one permission key. Emitted once per granted permission.</summary>
    public const string PermissionClaimType = "perm";
    public const string BranchClaimType = "branch";
    public const string AccessibleBranchesClaimType = "branches";
    public const string DepartmentClaimType = "dept";
    public const string CustomerClaimType = "customer_id";
    public const string TimeZoneClaimType = "tz";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name);

    public Guid? BranchId =>
        Guid.TryParse(Principal?.FindFirstValue(BranchClaimType), out var id) ? id : null;

    public string? TimeZoneId => Principal?.FindFirstValue(TimeZoneClaimType);

    public IReadOnlyCollection<Guid> AccessibleBranchIds => ParseGuidList(AccessibleBranchesClaimType);

    public IReadOnlyCollection<Guid> DepartmentIds => ParseGuidList(DepartmentClaimType);

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(PermissionClaimType).Select(c => c.Value).ToHashSet(StringComparer.Ordinal)
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public Guid? CustomerId =>
        Guid.TryParse(Principal?.FindFirstValue(CustomerClaimType), out var id) ? id : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool HasPermission(string permissionKey) =>
        Permissions.Contains(permissionKey);

    private IReadOnlyCollection<Guid> ParseGuidList(string claimType)
    {
        var raw = Principal?.FindAll(claimType).Select(c => c.Value).ToList();
        if (raw is null || raw.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return raw
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();
    }
}
