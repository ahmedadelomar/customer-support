using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Moves a ticket to another department (Platform / Departments, teams and queue scoping).
/// </summary>
/// <remarks>
/// Two rules make this different from a reassignment. The assignee is cleared, because the receiving
/// department picks its own owner and leaving the old one holding it hides the ticket from the new
/// queue's triage. And the SLA clock is NOT touched: internal routing is not the customer's problem,
/// and restarting the clock on transfer is the classic way to make breaches disappear from reports.
/// </remarks>
[RequirePermission(Permissions.Tickets.Assign)]
public record TransferTicketCommand : IRequest
{
    public Guid TicketId { get; init; }
    public Guid DepartmentId { get; init; }
    public Guid? TeamId { get; init; }
    public string? Reason { get; init; }
}

public class TransferTicketCommandValidator : AbstractValidator<TransferTicketCommand>
{
    public TransferTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public class TransferTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    INotificationDispatcher notifications)
    : IRequestHandler<TransferTicketCommand>
{
    public async Task Handle(TransferTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.Include(t => t.Status)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        TicketReadOnlyGuard.EnsureEditable(ticket, ticket.Status.IsTerminal);

        var target = await db.Departments
            .FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.DepartmentId);

        if (!target.IsActive)
        {
            throw new ConflictException("Tickets cannot be transferred into an inactive department.");
        }

        if (ticket.DepartmentId == request.DepartmentId && ticket.AssignedTeamId == request.TeamId)
        {
            return;
        }

        if (request.TeamId is { } teamId)
        {
            var teamBelongs = await db.Teams
                .AnyAsync(t => t.Id == teamId && t.DepartmentId == request.DepartmentId && t.IsActive, cancellationToken);

            if (!teamBelongs)
            {
                throw new ConflictException("The selected team does not belong to the destination department.");
            }
        }

        // Both names are read BEFORE the write so the event records what a person would have seen at
        // the time — a later rename must not rewrite history.
        var previousDepartmentId = ticket.DepartmentId;
        var previousName = previousDepartmentId is null
            ? null
            : await db.Departments
                .Where(d => d.Id == previousDepartmentId)
                .Select(d => d.Name.En)
                .FirstOrDefaultAsync(cancellationToken);

        var previousAssigneeId = ticket.AssignedAgentId;

        ticket.DepartmentId = request.DepartmentId;
        ticket.AssignedTeamId = request.TeamId;
        ticket.AssignedAgentId = null;
        ticket.AssignedAt = null;

        events.Record(ticket.Id, TicketEventType.DepartmentChanged,
            field: nameof(Ticket.DepartmentId),
            oldValue: previousDepartmentId?.ToString(),
            newValue: request.DepartmentId.ToString(),
            oldDisplay: previousName,
            newDisplay: target.Name.En,
            metadataJson: string.IsNullOrWhiteSpace(request.Reason)
                ? null
                : JsonSerializer.Serialize(new { request.Reason }));

        await db.SaveChangesAsync(cancellationToken);

        // The previous owner loses the ticket from their queue without having done anything; tell them.
        if (previousAssigneeId is { } assigneeId && assigneeId != currentUser.UserId)
        {
            await notifications.DispatchAsync(
                assigneeId,
                "ticket.transferred",
                "A ticket you owned was transferred",
                "تم تحويل تذكرة كنت مسؤولاً عنها",
                $"Ticket {ticket.Number} moved to {target.Name.En} and is no longer assigned to you.",
                $"تم نقل التذكرة {ticket.Number} إلى {target.Name.Ar} ولم تعد مسندة إليك.",
                link: $"/agent/tickets/{ticket.Id}",
                ct: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
