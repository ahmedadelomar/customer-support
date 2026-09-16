using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>
/// Raises a new ticket. Appends a <see cref="TicketEventType.Created"/> event and an inbound
/// <see cref="Domain.Customers.Interaction"/> in the same transaction, and seeds the conversation
/// thread with the description as the first (inbound, customer) message.
/// </summary>
[RequirePermission(Permissions.Tickets.Create)]
public record CreateTicketCommand : IRequest<Guid>
{
    public Guid CustomerId { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public Guid? PriorityId { get; init; }
    public Guid? DepartmentId { get; init; }
    public ChannelKey Channel { get; init; } = ChannelKey.Email;
}

public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public class CreateTicketCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IReferenceNumberGenerator numbers,
    ITicketEventRecorder events,
    IInteractionRecorder interactions,
    ISlaEngine sla,
    IAssignmentEngine assignment,
    IDateTimeProvider clock,
    ILogger<CreateTicketCommandHandler> logger)
    : IRequestHandler<CreateTicketCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .WhereBranchAccessible(currentUser)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Customers.Customer), request.CustomerId);

        if (customer.IsBlocked)
        {
            throw new ConflictException(
                $"Customer {customer.Code} is blocked and cannot raise new tickets. Reason: {customer.BlockedReason}");
        }

        var category = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketCategory), request.CategoryId);

        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.IsDefault, cancellationToken)
            ?? throw new InvalidOperationException("No default ticket status is configured.");

        var priorityId = request.PriorityId ?? category.DefaultPriorityId ?? await DefaultPriorityIdAsync(cancellationToken);
        var departmentId = request.DepartmentId ?? category.DefaultDepartmentId
            ?? await DefaultDepartmentIdAsync(customer.BranchId, cancellationToken);

        var ticket = new Ticket
        {
            Number = await numbers.NextTicketNumberAsync(cancellationToken),
            CustomerId = customer.Id,
            BranchId = customer.BranchId ?? currentUser.BranchId,
            Subject = request.Subject,
            Description = request.Description,
            Language = customer.PreferredLanguage,
            CategoryId = category.Id,
            PriorityId = priorityId,
            StatusId = status.Id,
            Channel = request.Channel,
            DepartmentId = departmentId,
        };

        db.Tickets.Add(ticket);

        ticket.Messages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = request.Channel,
            Direction = MessageDirection.Inbound,
            AuthorType = MessageAuthorType.Customer,
            AuthorId = customer.Id,
            AuthorDisplayName = customer.DisplayName.For(customer.PreferredLanguage),
            Subject = ticket.Subject,
            BodyText = ticket.Description,
            SentAt = clock.UtcNow,
        });

        events.Record(ticket.Id, TicketEventType.Created);
        interactions.Record(
            customer.Id, request.Channel, MessageDirection.Inbound,
            ticket.Subject, Truncate(ticket.Description), ticket.Id, nameof(Ticket), ticket.Id);

        await db.SaveChangesAsync(cancellationToken);

        // After the save, so the engine reads committed state — it performs its own SaveChangesAsync
        // for the clocks and the denormalised due-date columns (CS-501).
        await sla.ApplyPolicyAsync(ticket.Id, cancellationToken);

        // Outside the creation transaction and never allowed to fail it (CS-502): a routing problem
        // is a staffing question to fix, not a reason a customer's ticket fails to exist.
        try
        {
            await assignment.AssignAsync(ticket.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automatic assignment failed for ticket {TicketId}.", ticket.Id);
        }

        return ticket.Id;
    }

    private async Task<Guid> DefaultPriorityIdAsync(CancellationToken ct)
    {
        var priority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket priority is configured.");
        return priority.Id;
    }

    /// <summary>
    /// The "system default department" the product rules call for. <c>Department</c> carries no
    /// explicit default flag yet (CS-1203 owns department administration) — the oldest active
    /// department for the branch is used as a stable, deterministic stand-in until that story adds
    /// one, falling back to the oldest active department in any branch.
    /// </summary>
    private async Task<Guid?> DefaultDepartmentIdAsync(Guid? branchId, CancellationToken ct)
    {
        var department = await db.Departments
            .Where(d => d.IsActive && d.BranchId == branchId)
            .OrderBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        department ??= await db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return department?.Id;
    }

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
