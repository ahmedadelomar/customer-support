using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>One assignment candidate with the load and availability the assignment picker shows inline.</summary>
public record AssignableAgentDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string AvailabilityStatus { get; init; } = string.Empty;
    public int OpenTickets { get; init; }
    public int? Cap { get; init; }
    public bool IsAvailable { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Candidate agents for one ticket: its team's members if it has a team, otherwise its department's
/// agents. Each carries the exact same capacity reading <see cref="Commands.AssignTicketCommand"/>
/// will use, via <see cref="IAgentCapacityService"/> — so a candidate shown as available never turns
/// out blocked a moment later for a reason the picker didn't already show.
/// </summary>
[RequirePermission(Permissions.Tickets.Assign)]
public record GetAssignableAgentsQuery(Guid TicketId) : IRequest<IReadOnlyList<AssignableAgentDto>>;

public class GetAssignableAgentsQueryHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAgentDirectory agents,
    IAgentCapacityService capacityService)
    : IRequestHandler<GetAssignableAgentsQuery, IReadOnlyList<AssignableAgentDto>>
{
    public async Task<IReadOnlyList<AssignableAgentDto>> Handle(
        GetAssignableAgentsQuery request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        List<Guid> candidateIds;
        if (ticket.AssignedTeamId is { } teamId)
        {
            candidateIds = await db.TeamMembers
                .Where(m => m.TeamId == teamId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);
        }
        else if (ticket.DepartmentId is { } departmentId)
        {
            candidateIds = (await agents.GetAgentIdsByDepartmentAsync(departmentId, cancellationToken)).ToList();
        }
        else
        {
            candidateIds = [];
        }

        var snapshots = await agents.GetManyAsync(candidateIds, cancellationToken);

        var results = new List<AssignableAgentDto>();
        foreach (var id in candidateIds)
        {
            if (!snapshots.TryGetValue(id, out var snapshot))
            {
                continue;
            }

            var capacity = await capacityService.CheckAsync(id, ticket.AssignedTeamId, cancellationToken);

            results.Add(new AssignableAgentDto
            {
                Id = id,
                NameEn = snapshot.DisplayName.En,
                NameAr = snapshot.DisplayName.Ar,
                AvailabilityStatus = snapshot.AvailabilityStatus,
                OpenTickets = capacity.OpenTickets,
                Cap = capacity.Cap,
                IsAvailable = capacity.IsAvailable,
                Warning = capacity.Warning,
            });
        }

        return results;
    }
}
