using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Categories;

/// <summary>
/// The full category tree, active and inactive, for the admin editor. Ordered by <c>Path</c> so a
/// parent always precedes its children — the client builds the tree from this flat, already-sorted
/// list without a second pass.
/// </summary>
[RequirePermission(Permissions.Tickets.ManageCategories)]
public record GetTicketCategoriesAdminQuery : IRequest<IReadOnlyList<TicketCategoryAdminDto>>;

public class GetTicketCategoriesAdminQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTicketCategoriesAdminQuery, IReadOnlyList<TicketCategoryAdminDto>>
{
    public async Task<IReadOnlyList<TicketCategoryAdminDto>> Handle(
        GetTicketCategoriesAdminQuery request, CancellationToken cancellationToken)
    {
        var categories = await db.TicketCategories.AsNoTracking()
            .OrderBy(c => c.Path)
            .ToListAsync(cancellationToken);

        var counts = await db.Tickets.AsNoTracking()
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, g => g.Count, cancellationToken);

        return categories.Select(c => new TicketCategoryAdminDto
        {
            Id = c.Id,
            ParentId = c.ParentId,
            Code = c.Code,
            NameEn = c.Name.En,
            NameAr = c.Name.Ar,
            Description = c.Description,
            Path = c.Path,
            Depth = c.Depth,
            DisplayOrder = c.DisplayOrder,
            DefaultPriorityId = c.DefaultPriorityId,
            DefaultDepartmentId = c.DefaultDepartmentId,
            DefaultSlaPolicyId = c.DefaultSlaPolicyId,
            IsVisibleInPortal = c.IsVisibleInPortal,
            IsActive = c.IsActive,
            TicketCount = counts.TryGetValue(c.Id, out var count) ? count : 0,
        }).ToList();
    }
}
