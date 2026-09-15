using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Manually escalates a ticket. Adds the department manager as a watcher (if not already one) and
/// notifies them plus every existing watcher — escalation is meant to be seen, not just logged.
/// </summary>
[RequirePermission(Permissions.Tickets.Escalate)]
public record EscalateTicketCommand : IRequest
{
    public Guid TicketId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class EscalateTicketCommandValidator : AbstractValidator<EscalateTicketCommand>
{
    public EscalateTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A reason is required to escalate a ticket.");
    }
}

public class EscalateTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketEventRecorder events,
    INotificationDispatcher notifications,
    IDateTimeProvider clock)
    : IRequestHandler<EscalateTicketCommand>
{
    public async Task Handle(EscalateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .Include(t => t.Watchers)
            .Include(t => t.Status)
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        TicketReadOnlyGuard.EnsureEditable(ticket, ticket.Status.IsTerminal);

        ticket.EscalationLevel += 1;
        ticket.EscalatedAt = clock.UtcNow;

        events.Record(ticket.Id, TicketEventType.Escalated,
            field: nameof(Ticket.EscalationLevel),
            newValue: ticket.EscalationLevel.ToString(),
            metadataJson: JsonSerializer.Serialize(new { request.Reason }));

        var notifyUserIds = new HashSet<Guid>(ticket.Watchers.Select(w => w.UserId));

        if (ticket.DepartmentId is { } departmentId)
        {
            var managerId = await db.Departments
                .Where(d => d.Id == departmentId)
                .Select(d => d.ManagerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (managerId is { } manager && notifyUserIds.Add(manager))
            {
                ticket.Watchers.Add(new TicketWatcher
                {
                    TicketId = ticket.Id,
                    UserId = manager,
                    AddedByAutomation = true,
                    AddedAt = clock.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var userId in notifyUserIds)
        {
            await notifications.DispatchAsync(
                userId,
                "ticket.escalated",
                "A ticket was escalated",
                "تم تصعيد تذكرة",
                $"Ticket {ticket.Number} was escalated to level {ticket.EscalationLevel}: {request.Reason}",
                $"تم تصعيد التذكرة {ticket.Number} إلى المستوى {ticket.EscalationLevel}: {request.Reason}",
                link: $"/agent/tickets/{ticket.Id}",
                severity: "Warning",
                ct: cancellationToken);
        }

        if (notifyUserIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
