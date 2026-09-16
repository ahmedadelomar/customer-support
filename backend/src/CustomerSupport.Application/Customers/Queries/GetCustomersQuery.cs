using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Localization;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Queries;

/// <summary>
/// Paged, filtered customer list (Customer Management / Customer profiles).
/// Branch scoping is applied by the global query filter, not here.
/// </summary>
[RequirePermission(Permissions.Customers.View)]
public class GetCustomersQuery : PagedQuery, IRequest<PagedResult<CustomerListItemDto>>
{
    public CustomerType? Type { get; set; }
    public string? Tier { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsBlocked { get; set; }
    public Guid? AccountManagerId { get; set; }

    /// <summary>Restrict to customers with at least one non-terminal ticket.</summary>
    public bool? HasOpenTickets { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
}

public class GetCustomersQueryHandler(IAppDbContext db)
    : IRequestHandler<GetCustomersQuery, PagedResult<CustomerListItemDto>>
{
    /// <summary>Columns a caller may sort by. Anything else falls back to newest first.</summary>
    private static readonly string[] SortableColumns =
        ["code", "displayname", "primaryemail", "tier", "createdat", "lastinteractionat", "satisfactionscore"];

    public async Task<PagedResult<CustomerListItemDto>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            // Arabic spelling variants are typed interchangeably — "أحمد" and "احمد" are the same
            // name. The term is folded here and the stored Arabic name is folded in SQL by the
            // chained REPLACE below, so the match works whichever spelling either side used.
            // Both providers translate string.Replace, so this needs no collation change.
            var folded = ArabicText.Normalize(term);

            query = query.Where(c =>
                EF.Functions.Like(c.Code, $"%{term}%") ||
                EF.Functions.Like(c.DisplayName.En, $"%{term}%") ||
                EF.Functions.Like(c.DisplayName.Ar, $"%{term}%") ||
                EF.Functions.Like(
                    c.DisplayName.Ar
                        .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")
                        .Replace("ة", "ه").Replace("ى", "ي").Replace("ـ", ""),
                    $"%{folded}%") ||
                (c.PrimaryEmail != null && EF.Functions.Like(c.PrimaryEmail, $"%{term}%")) ||
                (c.PrimaryPhone != null && EF.Functions.Like(c.PrimaryPhone, $"%{term}%")) ||
                (c.NationalIdOrCr != null && EF.Functions.Like(c.NationalIdOrCr, $"%{term}%")));
        }

        if (request.Type is not null) query = query.Where(c => c.Type == request.Type);
        if (!string.IsNullOrWhiteSpace(request.Tier)) query = query.Where(c => c.Tier == request.Tier);
        if (request.IsActive is not null) query = query.Where(c => c.IsActive == request.IsActive);
        if (request.IsBlocked is not null) query = query.Where(c => c.IsBlocked == request.IsBlocked);
        if (request.AccountManagerId is not null) query = query.Where(c => c.AccountManagerId == request.AccountManagerId);
        if (request.CreatedFrom is not null) query = query.Where(c => c.CreatedAt >= request.CreatedFrom);
        if (request.CreatedTo is not null) query = query.Where(c => c.CreatedAt <= request.CreatedTo);

        // Open-ticket count as a correlated subquery rather than a group-join.
        // A group-join with DefaultIfEmpty() yields a nullable count that EF cannot materialise into
        // a non-nullable int ("Nullable object must have a value"), and this reads better besides.
        if (request.HasOpenTickets is true)
        {
            query = query.Where(c => db.Tickets.Any(t => t.CustomerId == c.Id && !t.Status.IsTerminal));
        }
        else if (request.HasOpenTickets is false)
        {
            query = query.Where(c => !db.Tickets.Any(t => t.CustomerId == c.Id && !t.Status.IsTerminal));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortBy = (request.SortBy ?? string.Empty).ToLowerInvariant();
        if (!SortableColumns.Contains(sortBy)) sortBy = "createdat";
        var desc = request.SortDescending || sortBy == "createdat";

        query = (sortBy, desc) switch
        {
            ("code", false) => query.OrderBy(c => c.Code),
            ("code", true) => query.OrderByDescending(c => c.Code),
            ("displayname", false) => query.OrderBy(c => c.DisplayName.En),
            ("displayname", true) => query.OrderByDescending(c => c.DisplayName.En),
            ("primaryemail", false) => query.OrderBy(c => c.PrimaryEmail),
            ("primaryemail", true) => query.OrderByDescending(c => c.PrimaryEmail),
            ("tier", false) => query.OrderBy(c => c.Tier),
            ("tier", true) => query.OrderByDescending(c => c.Tier),
            ("lastinteractionat", false) => query.OrderBy(c => c.LastInteractionAt),
            ("lastinteractionat", true) => query.OrderByDescending(c => c.LastInteractionAt),
            ("satisfactionscore", false) => query.OrderBy(c => c.SatisfactionScore),
            ("satisfactionscore", true) => query.OrderByDescending(c => c.SatisfactionScore),
            (_, false) => query.OrderBy(c => c.CreatedAt),
            (_, true) => query.OrderByDescending(c => c.CreatedAt),
        };

        // Fetch the page with its counts, then map to the DTO in memory. The mapping is a plain
        // method call, so keeping it off the translated expression tree avoids surprises.
        var page = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(c => new
            {
                Customer = c,
                OpenTickets = db.Tickets.Count(t => t.CustomerId == c.Id && !t.Status.IsTerminal),
            })
            .ToListAsync(cancellationToken);

        var items = page
            .Select(x => CustomerListItemDto.From(x.Customer, x.OpenTickets))
            .ToList();

        return PagedResult<CustomerListItemDto>.Create(items, request.Page, request.PageSize, totalCount);
    }
}
