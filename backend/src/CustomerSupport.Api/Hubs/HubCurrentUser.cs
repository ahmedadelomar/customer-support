using System.Security.Claims;
using CustomerSupport.Api.Services;
using CustomerSupport.Application.Common.Interfaces;

namespace CustomerSupport.Api.Hubs;

/// <summary>
/// <see cref="ICurrentUser"/> read directly from a <see cref="ClaimsPrincipal"/> rather than from
/// <c>IHttpContextAccessor</c>. A SignalR hub method invocation runs over an already-established
/// connection, not a fresh HTTP request through the accessor middleware, so
/// <see cref="CurrentUserService"/> cannot be reused as-is — <c>Context.User</c> is the reliable
/// source inside a hub instead. Reads the exact same claim types so authorization (e.g.
/// <c>WhereTicketVisible</c>) behaves identically to a controller action.
/// </summary>
public class HubCurrentUser(ClaimsPrincipal? principal) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => principal?.FindFirstValue(ClaimTypes.Name);

    public Guid? BranchId =>
        Guid.TryParse(principal?.FindFirstValue(CurrentUserService.BranchClaimType), out var id) ? id : null;

    public string? TimeZoneId => principal?.FindFirstValue(CurrentUserService.TimeZoneClaimType);

    public IReadOnlyCollection<Guid> AccessibleBranchIds => ParseGuidList(CurrentUserService.AccessibleBranchesClaimType);

    public IReadOnlyCollection<Guid> DepartmentIds => ParseGuidList(CurrentUserService.DepartmentClaimType);

    public IReadOnlyCollection<string> Permissions =>
        principal?.FindAll(CurrentUserService.PermissionClaimType).Select(c => c.Value).ToHashSet(StringComparer.Ordinal)
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public Guid? CustomerId =>
        Guid.TryParse(principal?.FindFirstValue(CurrentUserService.CustomerClaimType), out var id) ? id : null;

    public bool IsAuthenticated => principal?.Identity?.IsAuthenticated == true;

    public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);

    private IReadOnlyCollection<Guid> ParseGuidList(string claimType)
    {
        var raw = principal?.FindAll(claimType).Select(c => c.Value).ToList();
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
