using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.Auth.Dtos;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Issues JWTs and manages single-use refresh tokens.
/// </summary>
/// <remarks>
/// The access token carries one <c>perm</c> claim per granted permission, plus branch and department
/// scope. That is what lets <c>AuthorizationBehaviour</c> authorise a request without a database
/// round trip — at the cost that a permission change only takes effect on the next refresh.
/// </remarks>
public class TokenService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IDateTimeProvider clock,
    ILogger<TokenService> logger) : ITokenService
{
    /// <summary>One message for every failure mode, so the endpoint cannot enumerate accounts.</summary>
    private const string SignInFailed = "The username or password is incorrect.";

    public async Task<AuthResultDto> LoginAsync(
        string userName, string password, string? ip, CancellationToken ct = default)
    {
        var user = await userManager.FindByNameAsync(userName)
                   ?? await userManager.FindByEmailAsync(userName);

        // Every branch below returns the SAME exception. Do not "helpfully" distinguish them.
        if (user is null)
        {
            // Hash anyway so a missing user is not measurably faster than a wrong password.
            _ = userManager.PasswordHasher.HashPassword(new ApplicationUser(), password);
            throw new ForbiddenException(SignInFailed);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new ForbiddenException(SignInFailed);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            throw new ForbiddenException(SignInFailed);
        }

        if (!user.IsActive || user.IsDeleted)
        {
            throw new ForbiddenException(SignInFailed);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = clock.UtcNow;
        user.LastLoginIp = ip;

        return await IssueAsync(user, ip, ct);
    }

    public async Task<AuthResultDto> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);

        var stored = await db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new ForbiddenException("Invalid refresh token.");

        // A token presented after it was already rotated means the value leaked. Kill the chain.
        if (stored.RevokedAt is not null)
        {
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking all active tokens.", stored.UserId);

            await db.Set<RefreshToken>()
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.RevokedAt, clock.UtcNow)
                          .SetProperty(t => t.RevokedReason, "Token reuse detected"),
                    ct);

            throw new ForbiddenException("Invalid refresh token.");
        }

        if (stored.ExpiresAt <= clock.UtcNow)
        {
            throw new ForbiddenException("Invalid refresh token.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            throw new ForbiddenException("Invalid refresh token.");
        }

        stored.RevokedAt = clock.UtcNow;
        stored.RevokedReason = "Rotated";

        var result = await IssueAsync(user, ip, ct, rotatedFrom: stored);
        return result;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);

        // Idempotent: revoking an unknown or already-revoked token is not an error.
        await db.Set<RefreshToken>()
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, clock.UtcNow)
                      .SetProperty(t => t.RevokedReason, "Signed out"),
                ct);
    }

    private async Task<AuthResultDto> IssueAsync(
        ApplicationUser user, string? ip, CancellationToken ct, RefreshToken? rotatedFrom = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await ResolvePermissionsAsync(user, ct);
        var accessibleBranches = ParseBranchIds(user.AccessibleBranchIds);

        var minutes = configuration.GetValue("Jwt:AccessTokenMinutes", 60);
        var expiresAt = clock.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
        };

        if (user.BranchId is { } branchId)
        {
            claims.Add(new Claim("branch", branchId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.AccessibleBranchIds))
        {
            claims.Add(new Claim("branches", user.AccessibleBranchIds));
        }

        if (user.DepartmentId is { } departmentId)
        {
            claims.Add(new Claim("dept", departmentId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.TimeZoneId))
        {
            claims.Add(new Claim("tz", user.TimeZoneId));
        }

        if (user.CustomerId is { } customerId)
        {
            claims.Add(new Claim("customer_id", customerId.ToString()));
        }

        if (user.MustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // One claim per permission — CurrentUserService reads them exactly this way.
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        // Refresh token: random value returned once, only its hash persisted.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAt = clock.UtcNow.AddDays(configuration.GetValue("Jwt:RefreshTokenDays", 14)),
            CreatedAt = clock.UtcNow,
            CreatedByIp = ip,
        };

        db.Set<RefreshToken>().Add(refresh);
        await db.SaveChangesAsync(ct);

        if (rotatedFrom is not null)
        {
            rotatedFrom.ReplacedByTokenId = refresh.Id;
            await db.SaveChangesAsync(ct);
        }

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = raw,
            ExpiresAt = expiresAt,
            User = new AuthenticatedUserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                DisplayNameEn = user.DisplayName.En,
                DisplayNameAr = user.DisplayName.Ar,
                AvatarUrl = user.AvatarUrl,
                UserType = (int)user.UserType,
                BranchId = user.BranchId,
                AccessibleBranchIds = accessibleBranches,
                DepartmentId = user.DepartmentId,
                CustomerId = user.CustomerId,
                PreferredLanguage = user.PreferredLanguage,
                Roles = roles.ToList(),
                Permissions = permissions,
                MustChangePassword = user.MustChangePassword,
            },
        };
    }

    /// <summary>Resolves the distinct permission keys granted through every role the user holds.</summary>
    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(
        ApplicationUser user, CancellationToken ct)
    {
        var roleIds = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        if (roleIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        return await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);
    }

    private static List<Guid> ParseBranchIds(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
                 .Where(id => id is not null)
                 .Select(id => id!.Value)
                 .ToList();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
