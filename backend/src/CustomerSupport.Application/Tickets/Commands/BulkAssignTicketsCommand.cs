using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>One ticket's outcome within a bulk assignment — bulk assignment is per-ticket, never all-or-nothing.</summary>
public record BulkAssignItemResult(Guid TicketId, string Number, bool Succeeded, string? Error);

public record BulkAssignResultDto(int Succeeded, int Failed, IReadOnlyList<BulkAssignItemResult> Results);

/// <summary>
/// Assigns up to 100 tickets in one call. Each is wrapped in its own try/catch so one failure — a
/// ticket out of the caller's scope, or the agent hitting capacity partway through the batch — does
/// not abandon the rest.
/// </summary>
[RequirePermission(Permissions.Tickets.Assign)]
public record BulkAssignTicketsCommand : IRequest<BulkAssignResultDto>
{
    public IReadOnlyList<Guid> TicketIds { get; init; } = Array.Empty<Guid>();
    public Guid? AgentId { get; init; }
    public Guid? TeamId { get; init; }
    public bool Force { get; init; }
}

public class BulkAssignTicketsCommandValidator : AbstractValidator<BulkAssignTicketsCommand>
{
    public BulkAssignTicketsCommandValidator()
    {
        RuleFor(x => x.TicketIds).NotEmpty().Must(ids => ids.Count <= 100)
            .WithMessage("At most 100 tickets may be assigned in one request.");
        RuleFor(x => x).Must(x => x.AgentId is not null || x.TeamId is not null)
            .WithMessage("At least one of AgentId or TeamId must be provided.");
    }
}

public class BulkAssignTicketsCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    IAgentCapacityService capacityService,
    IAgentDirectory agents,
    INotificationDispatcher notifications,
    IDateTimeProvider clock)
    : IRequestHandler<BulkAssignTicketsCommand, BulkAssignResultDto>
{
    public async Task<BulkAssignResultDto> Handle(BulkAssignTicketsCommand request, CancellationToken cancellationToken)
    {
        Team? team = request.TeamId is { } teamId
            ? await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken)
            : null;

        var results = new List<BulkAssignItemResult>();

        foreach (var ticketId in request.TicketIds)
        {
            try
            {
                results.Add(await AssignOneAsync(request, ticketId, team, cancellationToken));
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or AssignmentWarningException)
            {
                results.Add(new BulkAssignItemResult(ticketId, string.Empty, false, ex.Message));
            }
        }

        return new BulkAssignResultDto(
            results.Count(r => r.Succeeded),
            results.Count(r => !r.Succeeded),
            results);
    }

    /// <summary>
    /// Re-checks capacity per ticket rather than once for the whole batch: assigning ticket 1 raises
    /// the agent's open count, which can legitimately push ticket 2 over their cap later in the same
    /// batch.
    /// </summary>
    private async Task<BulkAssignItemResult> AssignOneAsync(
        BulkAssignTicketsCommand request, Guid ticketId, Team? team, CancellationToken ct)
    {
        var ticket = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), ticketId);

        AgentSnapshot? agent = null;
        if (request.AgentId is { } agentId)
        {
            var capacity = await capacityService.CheckAsync(agentId, request.TeamId, ct);
            if (!capacity.IsAvailable && (capacity.IsHardBlock || !request.Force))
            {
                throw new ConflictException(capacity.Warning!);
            }

            agent = await agents.GetAsync(agentId, ct) ?? throw new NotFoundException("Agent", agentId);
        }

        var previousAgentId = ticket.AssignedAgentId;

        ticket.AssignedAgentId = request.AgentId;
        ticket.AssignedTeamId = request.TeamId;
        ticket.AssignedAt = clock.UtcNow;

        events.Record(ticket.Id, TicketEventType.Assigned,
            field: nameof(Ticket.AssignedAgentId),
            oldValue: previousAgentId?.ToString(),
            newValue: request.AgentId?.ToString(),
            newDisplay: agent?.DisplayName.En ?? team?.Name.En,
            triggeredByRule: "BulkAssign");

        await db.SaveChangesAsync(ct);

        if (agent is not null)
        {
            await TicketAssignmentNotifications.Assigned(notifications, ticket, agent.Id, ct);
        }

        if (previousAgentId is { } id && id != request.AgentId)
        {
            await TicketAssignmentNotifications.Unassigned(notifications, ticket, id, ct);
        }

        await db.SaveChangesAsync(ct);

        return new BulkAssignItemResult(ticket.Id, ticket.Number, true, null);
    }
}
