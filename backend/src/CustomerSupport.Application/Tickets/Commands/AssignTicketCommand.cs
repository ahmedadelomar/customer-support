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

/// <summary>
/// Assigns (or reassigns) a ticket to an agent, a team, or both. Sets <see cref="Ticket.AssignedAt"/>
/// and always appends an <see cref="TicketEventType.Assigned"/> event, even for a team-only
/// assignment — see the class remark on <see cref="AssignTicketCommand.AgentId"/> for what that means.
/// </summary>
[RequirePermission(Permissions.Tickets.Assign)]
public record AssignTicketCommand : IRequest
{
    public Guid TicketId { get; init; }

    /// <summary>Null leaves the ticket in the team's queue, unowned — a deliberate, valid state.</summary>
    public Guid? AgentId { get; init; }
    public Guid? TeamId { get; init; }

    /// <summary>Overrides an Away or at-capacity warning. Never overrides a deactivated agent.</summary>
    public bool Force { get; init; }
}

public class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x).Must(x => x.AgentId is not null || x.TeamId is not null)
            .WithMessage("At least one of AgentId or TeamId must be provided.");
    }
}

public class AssignTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    IAgentCapacityService capacityService,
    IAgentDirectory agents,
    INotificationDispatcher notifications,
    IDateTimeProvider clock)
    : IRequestHandler<AssignTicketCommand>
{
    public async Task Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        AgentSnapshot? newAgent = null;
        if (request.AgentId is { } agentId)
        {
            var capacity = await capacityService.CheckAsync(agentId, request.TeamId, cancellationToken);
            if (!capacity.IsAvailable && (capacity.IsHardBlock || !request.Force))
            {
                throw new AssignmentWarningException(capacity.Warning!, capacity.IsHardBlock, capacity.OpenTickets, capacity.Cap);
            }

            newAgent = await agents.GetAsync(agentId, cancellationToken)
                ?? throw new NotFoundException("Agent", agentId);
        }

        Team? team = request.TeamId is { } teamId
            ? await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken)
                ?? throw new NotFoundException(nameof(Team), teamId)
            : null;

        var previousAgentId = ticket.AssignedAgentId;
        var previousAgent = previousAgentId is { } prevId ? await agents.GetAsync(prevId, cancellationToken) : null;

        ticket.AssignedAgentId = request.AgentId;
        ticket.AssignedTeamId = request.TeamId;
        ticket.AssignedAt = clock.UtcNow;

        events.Record(ticket.Id, TicketEventType.Assigned,
            field: nameof(Ticket.AssignedAgentId),
            oldValue: previousAgentId?.ToString(),
            newValue: request.AgentId?.ToString(),
            oldDisplay: previousAgent?.DisplayName.En,
            newDisplay: newAgent?.DisplayName.En ?? team?.Name.En);

        await db.SaveChangesAsync(cancellationToken);

        if (newAgent is not null)
        {
            await TicketAssignmentNotifications.Assigned(notifications, ticket, newAgent.Id, cancellationToken);
        }

        if (previousAgentId is { } id && id != request.AgentId)
        {
            await TicketAssignmentNotifications.Unassigned(notifications, ticket, id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
