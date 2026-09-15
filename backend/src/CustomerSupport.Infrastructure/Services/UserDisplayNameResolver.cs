using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Reads display names straight off the Identity table. Lives in Infrastructure because
/// <c>ApplicationUser</c> does — see the remark on <see cref="IUserDisplayNameResolver"/> for why
/// this indirection exists instead of just exposing a <c>Users</c> set on <c>IAppDbContext</c>.
/// </summary>
public class UserDisplayNameResolver(AppDbContext db) : IUserDisplayNameResolver
{
    public async Task<IReadOnlyDictionary<Guid, LocalizedText>> ResolveAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, LocalizedText>();
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }
}
