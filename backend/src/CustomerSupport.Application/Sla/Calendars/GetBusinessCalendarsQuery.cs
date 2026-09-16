using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Sla.Calendars;

/// <summary>Every calendar, for the admin editor and the SLA policy calendar picker.</summary>
[RequirePermission(Permissions.Sla.ManageCalendars)]
public record GetBusinessCalendarsQuery : IRequest<IReadOnlyList<BusinessCalendarDto>>;

public class GetBusinessCalendarsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetBusinessCalendarsQuery, IReadOnlyList<BusinessCalendarDto>>
{
    public async Task<IReadOnlyList<BusinessCalendarDto>> Handle(GetBusinessCalendarsQuery request, CancellationToken cancellationToken)
    {
        var calendars = await db.BusinessCalendars.AsNoTracking()
            .Include(c => c.BusinessHours)
            .Include(c => c.Holidays)
            .OrderBy(c => c.Name.En)
            .ToListAsync(cancellationToken);

        var inUseIds = await db.SlaPolicies.AsNoTracking()
            .Select(p => p.BusinessCalendarId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return calendars.Select(c => new BusinessCalendarDto
        {
            Id = c.Id,
            NameEn = c.Name.En,
            NameAr = c.Name.Ar,
            TimeZoneId = c.TimeZoneId,
            IsTwentyFourSeven = c.IsTwentyFourSeven,
            IsDefault = c.IsDefault,
            IsInUse = inUseIds.Contains(c.Id),
            BusinessHours = c.BusinessHours
                .OrderBy(h => h.DayOfWeek).ThenBy(h => h.StartTime)
                .Select(h => new BusinessHourDto(h.DayOfWeek, h.StartTime, h.EndTime))
                .ToList(),
            Holidays = c.Holidays
                .OrderBy(h => h.Date)
                .Select(h => new HolidayDto(h.Date, h.Name.En, h.Name.Ar, h.IsRecurringAnnually))
                .ToList(),
        }).ToList();
    }
}
