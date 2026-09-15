using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Application.Tickets.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>Full ticket, its customer panel and its properties, for the detail screen.</summary>
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

        var customerPanel = await BuildCustomerPanelAsync(customer, ticket.Id, cancellationToken);

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
            Customer = customerPanel,
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

    /// <summary>
    /// Builds the ticket screen's customer panel (Agent Dashboard / Customer information) — identity,
    /// tier, blocked state, contacts, other open tickets and pinned notes, all in this one query so
    /// the ticket screen never fires a second round trip for it. Populated only when the caller holds
    /// <c>customers.view</c>; otherwise only the display name is returned, per the story's own
    /// degrade-rather-than-error rule — this lives in the projection, not the controller, so no future
    /// caller of this query can accidentally bypass the gate.
    /// </summary>
    private async Task<CustomerPanelDto> BuildCustomerPanelAsync(
        Domain.Customers.Customer customer, Guid currentTicketId, CancellationToken ct)
    {
        var canViewFull = currentUser.HasPermission(Permissions.Customers.View);

        if (!canViewFull)
        {
            return new CustomerPanelDto
            {
                Id = customer.Id,
                DisplayNameEn = customer.DisplayName.En,
                DisplayNameAr = customer.DisplayName.Ar,
                CanViewFull = false,
            };
        }

        var openTicketCount = await db.Tickets
            .CountAsync(t => t.CustomerId == customer.Id && !t.Status.IsTerminal, ct);

        var otherOpenTicketsQuery = db.Tickets.AsNoTracking()
            .Where(t => t.CustomerId == customer.Id && t.Id != currentTicketId && !t.Status.IsTerminal);

        var otherOpenTicketCount = await otherOpenTicketsQuery.CountAsync(ct);

        var otherOpenTickets = await otherOpenTicketsQuery
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new CustomerPanelOtherTicketDto
            {
                Id = t.Id,
                Number = t.Number,
                Subject = t.Subject,
                StatusNameEn = t.Status.Name.En,
                StatusNameAr = t.Status.Name.Ar,
                StatusColorHex = t.Status.ColorHex,
            })
            .ToListAsync(ct);

        var contacts = await db.CustomerContacts.AsNoTracking()
            .Where(c => c.CustomerId == customer.Id)
            .OrderByDescending(c => c.IsPrimary)
            .Select(c => new CustomerContactDto
            {
                Id = c.Id,
                Type = c.Type,
                Value = c.Value,
                Label = c.Label,
                IsPrimary = c.IsPrimary,
                IsVerified = c.IsVerified,
                AllowNotifications = c.AllowNotifications,
                CountryCode = c.CountryCode,
                City = c.City,
                AddressLine = c.AddressLine,
                PostalCode = c.PostalCode,
            })
            .ToListAsync(ct);

        var pinnedNoteRows = await db.CustomerNotes.AsNoTracking()
            .Where(n => n.CustomerId == customer.Id && n.IsPinned)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.Body, n.CreatedById, n.CreatedAt })
            .ToListAsync(ct);

        var authorIds = pinnedNoteRows.Where(n => n.CreatedById is not null).Select(n => n.CreatedById!.Value).Distinct();
        var authorNames = await userNames.ResolveAsync(authorIds, ct);

        var pinnedNotes = pinnedNoteRows.Select(n => new CustomerPanelNoteDto
        {
            Id = n.Id,
            Body = n.Body,
            AuthorNameEn = n.CreatedById is { } authorId && authorNames.TryGetValue(authorId, out var name) ? name.En : null,
            AuthorNameAr = n.CreatedById is { } authorId2 && authorNames.TryGetValue(authorId2, out var name2) ? name2.Ar : null,
            CreatedAt = n.CreatedAt,
        }).ToList();

        return new CustomerPanelDto
        {
            Id = customer.Id,
            DisplayNameEn = customer.DisplayName.En,
            DisplayNameAr = customer.DisplayName.Ar,
            CanViewFull = true,
            Code = customer.Code,
            Tier = customer.Tier,
            PreferredLanguage = customer.PreferredLanguage,
            PreferredChannel = customer.PreferredChannel,
            IsBlocked = customer.IsBlocked,
            BlockedReason = customer.BlockedReason,
            SatisfactionScore = customer.SatisfactionScore,
            LastInteractionAt = customer.LastInteractionAt,
            OpenTicketCount = openTicketCount,
            Contacts = contacts,
            OtherOpenTickets = otherOpenTickets,
            OtherOpenTicketCount = otherOpenTicketCount,
            PinnedNotes = pinnedNotes,
            CanEdit = currentUser.HasPermission(Permissions.Customers.Update),
        };
    }
}
