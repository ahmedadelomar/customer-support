using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Tickets.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>
/// Categories, priorities, statuses and departments for the create form's pickers and the list's
/// filters. Fetched once and cached client-side rather than resolved per row.
/// </summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetTicketLookupsQuery : IRequest<TicketLookupsDto>;

public class GetTicketLookupsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTicketLookupsQuery, TicketLookupsDto>
{
    public async Task<TicketLookupsDto> Handle(GetTicketLookupsQuery request, CancellationToken cancellationToken)
    {
        var categories = await db.TicketCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Path)
            .Select(c => new TicketCategoryLookupDto
            {
                Id = c.Id,
                Code = c.Code,
                NameEn = c.Name.En,
                NameAr = c.Name.Ar,
                ParentId = c.ParentId,
                Depth = c.Depth,
                Path = c.Path,
                DefaultPriorityId = c.DefaultPriorityId,
                DefaultDepartmentId = c.DefaultDepartmentId,
            })
            .ToListAsync(cancellationToken);

        var priorities = await db.TicketPriorities.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Level)
            .Select(p => new TicketPriorityLookupDto
            {
                Id = p.Id,
                Code = p.Code,
                NameEn = p.Name.En,
                NameAr = p.Name.Ar,
                Level = p.Level,
                ColorHex = p.ColorHex,
                IsDefault = p.IsDefault,
            })
            .ToListAsync(cancellationToken);

        var statuses = await db.TicketStatuses.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new TicketStatusLookupDto
            {
                Id = s.Id,
                Code = s.Code,
                NameEn = s.Name.En,
                NameAr = s.Name.Ar,
                Kind = s.Kind,
                ColorHex = s.ColorHex,
                IsTerminal = s.IsTerminal,
                PausesSla = s.PausesSla,
                IsDefault = s.IsDefault,
            })
            .ToListAsync(cancellationToken);

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Code)
            .Select(d => new DepartmentLookupDto
            {
                Id = d.Id,
                Code = d.Code,
                NameEn = d.Name.En,
                NameAr = d.Name.Ar,
            })
            .ToListAsync(cancellationToken);

        return new TicketLookupsDto
        {
            Categories = categories,
            Priorities = priorities,
            Statuses = statuses,
            Departments = departments,
        };
    }
}
