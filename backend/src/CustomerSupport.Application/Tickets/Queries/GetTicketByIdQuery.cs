using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>Full ticket, its customer summary and its properties, for the detail screen.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetTicketByIdQuery(Guid Id) : IRequest<TicketDetailDto>;

public class GetTicketByIdQueryHandler(IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetTicketByIdQuery, TicketDetailDto>
{
    public async Task<TicketDetailDto> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .WhereBranchAccessible(currentUser)
            .WhereTicketVisible(currentUser)
            .Include(t => t.Tags).ThenInclude(tt => tt.Tag)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.Id);

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == ticket.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Customers.Customer), ticket.CustomerId);

        var openTicketCount = await db.Tickets
            .CountAsync(t => t.CustomerId == customer.Id && !t.Status.IsTerminal, cancellationToken);

        var category = await db.TicketCategories.AsNoTracking()
            .FirstAsync(c => c.Id == ticket.CategoryId, cancellationToken);
        var priority = await db.TicketPriorities.AsNoTracking()
            .FirstAsync(p => p.Id == ticket.PriorityId, cancellationToken);
        var status = await db.TicketStatuses.AsNoTracking()
            .FirstAsync(s => s.Id == ticket.StatusId, cancellationToken);

        Domain.Organization.Department? department = ticket.DepartmentId is { } departmentId
            ? await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            : null;

        var namedUserIds = new List<Guid>();
        if (ticket.AssignedAgentId is { } agentId) namedUserIds.Add(agentId);
        var names = await userNames.ResolveAsync(namedUserIds, cancellationToken);

        return new TicketDetailDto
        {
            Id = ticket.Id,
            Number = ticket.Number,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Language = ticket.Language,
            Customer = new TicketCustomerSummaryDto
            {
                Id = customer.Id,
                Code = customer.Code,
                DisplayNameEn = customer.DisplayName.En,
                DisplayNameAr = customer.DisplayName.Ar,
                Tier = customer.Tier,
                PrimaryEmail = customer.PrimaryEmail,
                PrimaryPhone = customer.PrimaryPhone,
                IsBlocked = customer.IsBlocked,
                OpenTicketCount = openTicketCount,
            },
            CategoryId = category.Id,
            CategoryNameEn = category.Name.En,
            CategoryNameAr = category.Name.Ar,
            PriorityId = priority.Id,
            PriorityNameEn = priority.Name.En,
            PriorityNameAr = priority.Name.Ar,
            PriorityColorHex = priority.ColorHex,
            StatusId = status.Id,
            StatusNameEn = status.Name.En,
            StatusNameAr = status.Name.Ar,
            StatusColorHex = status.ColorHex,
            StatusKind = status.Kind,
            IsTerminal = status.IsTerminal,
            Channel = ticket.Channel,
            DepartmentId = ticket.DepartmentId,
            DepartmentNameEn = department?.Name.En,
            DepartmentNameAr = department?.Name.Ar,
            AssignedAgentId = ticket.AssignedAgentId,
            AssignedAgentNameEn = ticket.AssignedAgentId is { } aId && names.TryGetValue(aId, out var n) ? n.En : null,
            AssignedAgentNameAr = ticket.AssignedAgentId is { } aId2 && names.TryGetValue(aId2, out var n2) ? n2.Ar : null,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            IsFirstResponseBreached = ticket.IsFirstResponseBreached,
            IsResolutionBreached = ticket.IsResolutionBreached,
            ResolvedAt = ticket.ResolvedAt,
            ResolutionNote = ticket.ResolutionNote,
            ClosedAt = ticket.ClosedAt,
            ReopenCount = ticket.ReopenCount,
            CustomerReplyCount = ticket.CustomerReplyCount,
            EscalationLevel = ticket.EscalationLevel,
            EscalatedAt = ticket.EscalatedAt,
            MergedIntoTicketId = ticket.MergedIntoTicketId,
            Tags = ticket.Tags.Select(tt => new TicketTagDto
            {
                Id = tt.Tag.Id,
                Name = tt.Tag.Name,
                ColorHex = tt.Tag.ColorHex,
            }).ToList(),
            CreatedAt = ticket.CreatedAt,
            ModifiedAt = ticket.ModifiedAt,
            CanUpdate = currentUser.HasPermission(Permissions.Tickets.Update),
            CanReply = currentUser.HasPermission(Permissions.Tickets.Reply),
            CanAddInternalNote = currentUser.HasPermission(Permissions.Tickets.InternalNote),
            CanMerge = currentUser.HasPermission(Permissions.Tickets.Merge),
            CanAssign = currentUser.HasPermission(Permissions.Tickets.Assign),
            CanChangeStatus = currentUser.HasPermission(Permissions.Tickets.ChangeStatus),
            CanEscalate = currentUser.HasPermission(Permissions.Tickets.Escalate),
        };
    }
}
