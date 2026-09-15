using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>
/// Filters shared by the ticket list and its KPI tiles. A plain base class, not a request itself —
/// <see cref="GetTicketsQuery"/> and <see cref="GetTicketStatisticsQuery"/> each declare their own
/// <c>IRequest&lt;T&gt;</c> so MediatR's response type stays unambiguous for each.
/// </summary>
public abstract class TicketFilterQuery : PagedQuery
{
    /// <summary>"mine", "team", "unassigned" or "all" (default).</summary>
    public string? Assignment { get; set; }
    public Guid? StatusId { get; set; }
    public TicketStatusKind? StatusKind { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? CategoryId { get; set; }
    public ChannelKey? Channel { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? CustomerId { get; set; }
    /// <summary>"breached", "duesoon" or "ontrack".</summary>
    public string? SlaState { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? Tag { get; set; }
    /// <summary>Open tickets whose resolution is due before the end of today (server clock) — same definition the dashboard's tile and this list's own KPI tile already use.</summary>
    public bool? DueToday { get; set; }
}

/// <summary>
/// Paged, filtered ticket list — the screen agents spend their day in (Ticket Management / Create
/// and track tickets). Follows <c>GetCustomersQuery</c>, plus the two scoping calls every ticket
/// query must apply.
/// </summary>
[RequirePermission(Permissions.Tickets.View)]
public class GetTicketsQuery : TicketFilterQuery, IRequest<PagedResult<TicketListItemDto>>;

public class GetTicketsQueryHandler(IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetTicketsQuery, PagedResult<TicketListItemDto>>
{
    private static readonly string[] SortableColumns =
        ["number", "subject", "createdat", "prioritylevel", "resolutiondueat", "lastcustomerreplyat"];

    public async Task<PagedResult<TicketListItemDto>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Tickets.AsNoTracking()
            .WhereBranchAccessible(currentUser)
            .WhereTicketVisible(currentUser);

        query = await TicketFilters.ApplyAsync(query, request, currentUser, db, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t =>
                EF.Functions.Like(t.Number, $"%{term}%") ||
                EF.Functions.Like(t.Subject, $"%{term}%") ||
                EF.Functions.Like(t.Customer.DisplayName.En, $"%{term}%") ||
                EF.Functions.Like(t.Customer.DisplayName.Ar, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortBy = (request.SortBy ?? string.Empty).ToLowerInvariant();
        if (!SortableColumns.Contains(sortBy)) sortBy = "createdat";
        var desc = request.SortDescending || sortBy == "createdat";

        query = (sortBy, desc) switch
        {
            ("number", false) => query.OrderBy(t => t.Number),
            ("number", true) => query.OrderByDescending(t => t.Number),
            ("subject", false) => query.OrderBy(t => t.Subject),
            ("subject", true) => query.OrderByDescending(t => t.Subject),
            ("prioritylevel", false) => query.OrderBy(t => t.Priority.Level),
            ("prioritylevel", true) => query.OrderByDescending(t => t.Priority.Level),
            ("resolutiondueat", false) => query.OrderBy(t => t.ResolutionDueAt),
            ("resolutiondueat", true) => query.OrderByDescending(t => t.ResolutionDueAt),
            ("lastcustomerreplyat", false) => query.OrderBy(t => t.LastCustomerReplyAt),
            ("lastcustomerreplyat", true) => query.OrderByDescending(t => t.LastCustomerReplyAt),
            (_, false) => query.OrderBy(t => t.CreatedAt),
            (_, true) => query.OrderByDescending(t => t.CreatedAt),
        };

        var rows = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(t => new
            {
                t.Id,
                t.Number,
                t.Subject,
                t.CustomerId,
                CustomerDisplayNameEn = t.Customer.DisplayName.En,
                CustomerDisplayNameAr = t.Customer.DisplayName.Ar,
                t.CategoryId,
                CategoryNameEn = t.Category.Name.En,
                CategoryNameAr = t.Category.Name.Ar,
                t.PriorityId,
                PriorityNameEn = t.Priority.Name.En,
                PriorityNameAr = t.Priority.Name.Ar,
                PriorityColorHex = t.Priority.ColorHex,
                t.StatusId,
                StatusNameEn = t.Status.Name.En,
                StatusNameAr = t.Status.Name.Ar,
                StatusColorHex = t.Status.ColorHex,
                StatusKind = t.Status.Kind,
                t.Channel,
                t.DepartmentId,
                t.AssignedAgentId,
                t.FirstResponseDueAt,
                t.ResolutionDueAt,
                t.IsFirstResponseBreached,
                t.IsResolutionBreached,
                t.LastCustomerReplyAt,
                t.LastAgentReplyAt,
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var agentIds = rows.Where(r => r.AssignedAgentId is not null).Select(r => r.AssignedAgentId!.Value).Distinct();
        var agentNames = await userNames.ResolveAsync(agentIds, cancellationToken);

        var items = rows.Select(row => new TicketListItemDto
        {
            Id = row.Id,
            Number = row.Number,
            Subject = row.Subject,
            CustomerId = row.CustomerId,
            CustomerDisplayNameEn = row.CustomerDisplayNameEn,
            CustomerDisplayNameAr = row.CustomerDisplayNameAr,
            CategoryId = row.CategoryId,
            CategoryNameEn = row.CategoryNameEn,
            CategoryNameAr = row.CategoryNameAr,
            PriorityId = row.PriorityId,
            PriorityNameEn = row.PriorityNameEn,
            PriorityNameAr = row.PriorityNameAr,
            PriorityColorHex = row.PriorityColorHex,
            StatusId = row.StatusId,
            StatusNameEn = row.StatusNameEn,
            StatusNameAr = row.StatusNameAr,
            StatusColorHex = row.StatusColorHex,
            StatusKind = row.StatusKind,
            Channel = row.Channel,
            DepartmentId = row.DepartmentId,
            AssignedAgentId = row.AssignedAgentId,
            AssignedAgentNameEn = row.AssignedAgentId is { } aId && agentNames.TryGetValue(aId, out var n) ? n.En : null,
            AssignedAgentNameAr = row.AssignedAgentId is { } aId2 && agentNames.TryGetValue(aId2, out var n2) ? n2.Ar : null,
            FirstResponseDueAt = row.FirstResponseDueAt,
            ResolutionDueAt = row.ResolutionDueAt,
            IsFirstResponseBreached = row.IsFirstResponseBreached,
            IsResolutionBreached = row.IsResolutionBreached,
            LastCustomerReplyAt = row.LastCustomerReplyAt,
            LastAgentReplyAt = row.LastAgentReplyAt,
            CreatedAt = row.CreatedAt,
        }).ToList();

        return PagedResult<TicketListItemDto>.Create(items, request.Page, request.PageSize, totalCount);
    }
}

/// <summary>Applies <see cref="TicketFilterQuery"/> to a ticket queryable. Shared by the list and the statistics query so the two can never drift apart.</summary>
public static class TicketFilters
{
    public static async Task<IQueryable<Ticket>> ApplyAsync(
        IQueryable<Ticket> query,
        TicketFilterQuery request,
        ICurrentUser currentUser,
        IAppDbContext db,
        CancellationToken ct)
    {
        if (request.Assignment == "team")
        {
            var teamIds = await db.TeamMembers
                .Where(m => m.UserId == currentUser.UserId && m.IsActive)
                .Select(m => m.TeamId)
                .ToListAsync(ct);

            query = query.Where(t => t.AssignedTeamId != null && teamIds.Contains(t.AssignedTeamId.Value));
        }
        else
        {
            query = request.Assignment switch
            {
                "mine" => query.Where(t => t.AssignedAgentId == currentUser.UserId),
                "unassigned" => query.Where(t => t.AssignedAgentId == null),
                _ => query,
            };
        }

        if (request.StatusId is { } statusId) query = query.Where(t => t.StatusId == statusId);
        if (request.StatusKind is { } statusKind) query = query.Where(t => t.Status.Kind == statusKind);
        if (request.PriorityId is { } priorityId) query = query.Where(t => t.PriorityId == priorityId);
        if (request.CategoryId is { } categoryId) query = query.Where(t => t.CategoryId == categoryId);
        if (request.Channel is { } channel) query = query.Where(t => t.Channel == channel);
        if (request.DepartmentId is { } departmentId) query = query.Where(t => t.DepartmentId == departmentId);
        if (request.CustomerId is { } customerId) query = query.Where(t => t.CustomerId == customerId);
        if (request.From is { } from) query = query.Where(t => t.CreatedAt >= from);
        if (request.To is { } to) query = query.Where(t => t.CreatedAt <= to);
        if (request.Tag is { } tag) query = query.Where(t => t.Tags.Any(x => x.TagId == tag));

        if (request.DueToday == true)
        {
            var endOfToday = DateTimeOffset.UtcNow.Date.AddDays(1);
            query = query.Where(t => !t.Status.IsTerminal && t.ResolutionDueAt != null && t.ResolutionDueAt < endOfToday);
        }

        // Precomputed rather than inlining `DateTimeOffset.UtcNow.AddHours(2)` in the LINQ predicate
        // below: the SQLite provider fails to translate that exact shape (a static-member-chain
        // expression compared against a nullable column with `<=`/`>`), throwing at query-compile
        // time with "could not be translated" — a local variable sidesteps it entirely.
        var slaWarningThreshold = DateTimeOffset.UtcNow.AddHours(2);

        query = request.SlaState switch
        {
            "breached" => query.Where(t => t.IsFirstResponseBreached || t.IsResolutionBreached),
            "duesoon" => query.Where(t =>
                !t.IsResolutionBreached && !t.IsFirstResponseBreached && !t.Status.IsTerminal &&
                t.ResolutionDueAt != null && t.ResolutionDueAt <= slaWarningThreshold),
            "ontrack" => query.Where(t =>
                !t.IsResolutionBreached && !t.IsFirstResponseBreached &&
                (t.ResolutionDueAt == null || t.ResolutionDueAt > slaWarningThreshold)),
            _ => query,
        };

        return query;
    }
}
