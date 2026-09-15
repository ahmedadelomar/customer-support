using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>Reads agent identity and availability straight off the Identity table. Lives in Infrastructure for the same reason <see cref="UserDisplayNameResolver"/> does.</summary>
public class AgentDirectory(AppDbContext db) : IAgentDirectory
{
    public async Task<AgentSnapshot?> GetAsync(Guid agentId, CancellationToken ct = default)
    {
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == agentId)
            .Select(u => new AgentSnapshot(u.Id, u.DisplayName, u.IsActive, u.AvailabilityStatus, u.MaxConcurrentTickets, u.DepartmentId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, AgentSnapshot>> GetManyAsync(
        IEnumerable<Guid> agentIds, CancellationToken ct = default)
    {
        var ids = agentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, AgentSnapshot>();
        }

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new AgentSnapshot(u.Id, u.DisplayName, u.IsActive, u.AvailabilityStatus, u.MaxConcurrentTickets, u.DepartmentId))
            .ToDictionaryAsync(a => a.Id, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAgentIdsByDepartmentAsync(Guid departmentId, CancellationToken ct = default)
    {
        return await db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == departmentId && u.IsActive && u.UserType == UserType.Agent)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }
}
