using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Assignment;

/// <summary>
/// Capacity and availability for one candidate assignment. <see cref="Cap"/> is null when unlimited.
/// <see cref="IsHardBlock"/> distinguishes a deactivated agent (never overridable) from Away or
/// at-capacity (a warning the caller may override with <c>Force = true</c>).
/// </summary>
public record CapacityResult(bool IsAvailable, bool IsHardBlock, int OpenTickets, int? Cap, string? Warning);

/// <summary>
/// The single capacity rule for assigning a ticket to an agent — used by manual assignment here and
/// meant to be reused by CS-502 (automatic assignment) rather than reimplemented, so the two can never
/// disagree about who is available.
/// </summary>
public interface IAgentCapacityService
{
    Task<CapacityResult> CheckAsync(Guid agentId, Guid? teamId, CancellationToken ct = default);
}

public class AgentCapacityService(IAppDbContext db, IAgentDirectory agents) : IAgentCapacityService
{
    public async Task<CapacityResult> CheckAsync(Guid agentId, Guid? teamId, CancellationToken ct = default)
    {
        var agent = await agents.GetAsync(agentId, ct)
            ?? throw new NotFoundException("Agent", agentId);

        var openCount = await db.Tickets
            .CountAsync(t => t.AssignedAgentId == agentId && !t.Status.IsTerminal, ct);

        var cap = await ResolveCapAsync(agentId, teamId, agent.MaxConcurrentTickets, ct);

        // A deactivated agent is a hard block: no cap or availability reading matters once the
        // account itself is gone.
        if (!agent.IsActive)
        {
            return new CapacityResult(false, true, openCount, cap, "This agent's account is deactivated.");
        }

        if (agent.AvailabilityStatus is "Away" or "Offline")
        {
            return new CapacityResult(false, false, openCount, cap, $"Agent is {agent.AvailabilityStatus}.");
        }

        if (cap is { } capValue && openCount >= capValue)
        {
            return new CapacityResult(false, false, openCount, cap, $"Agent is at capacity ({openCount}/{capValue}).");
        }

        return new CapacityResult(true, false, openCount, cap, null);
    }

    /// <summary>The team-member cap wins when the agent belongs to that team; otherwise the user-level cap. Either being zero means unlimited.</summary>
    private async Task<int?> ResolveCapAsync(Guid agentId, Guid? teamId, int userCap, CancellationToken ct)
    {
        if (teamId is { } tid)
        {
            var teamCap = await db.TeamMembers
                .Where(m => m.TeamId == tid && m.UserId == agentId)
                .Select(m => (int?)m.MaxConcurrentTickets)
                .FirstOrDefaultAsync(ct);

            if (teamCap is { } cap)
            {
                return cap == 0 ? null : cap;
            }
        }

        return userCap == 0 ? null : userCap;
    }
}
