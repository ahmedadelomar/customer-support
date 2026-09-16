using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Sla.Policies;

/// <summary>The full policy scale, active and inactive, for the admin editor.</summary>
[RequirePermission(Permissions.Sla.ManagePolicies)]
public record GetSlaPoliciesQuery : IRequest<IReadOnlyList<SlaPolicyDto>>;

public class GetSlaPoliciesQueryHandler(IAppDbContext db) : IRequestHandler<GetSlaPoliciesQuery, IReadOnlyList<SlaPolicyDto>>
{
    public async Task<IReadOnlyList<SlaPolicyDto>> Handle(GetSlaPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await db.SlaPolicies.AsNoTracking()
            .Include(p => p.BusinessCalendar)
            .Include(p => p.Targets)
            .Include(p => p.Conditions)
            .OrderBy(p => p.EvaluationOrder).ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        var priorities = await db.TicketPriorities.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return policies.Select(p => new SlaPolicyDto
        {
            Id = p.Id,
            NameEn = p.Name.En,
            NameAr = p.Name.Ar,
            Description = p.Description,
            BusinessCalendarId = p.BusinessCalendarId,
            CalendarNameEn = p.BusinessCalendar.Name.En,
            CalendarNameAr = p.BusinessCalendar.Name.Ar,
            EvaluationOrder = p.EvaluationOrder,
            IsDefault = p.IsDefault,
            IsActive = p.IsActive,
            WarningThresholdPercent = p.WarningThresholdPercent,
            PauseOnPendingCustomer = p.PauseOnPendingCustomer,
            Targets = p.Targets.Select(t => priorities.TryGetValue(t.PriorityId, out var priority)
                ? new SlaTargetDto(t.PriorityId, priority.Code, priority.Name.En, priority.Name.Ar, t.FirstResponseMinutes, t.ResolutionMinutes)
                : new SlaTargetDto(t.PriorityId, "", "", "", t.FirstResponseMinutes, t.ResolutionMinutes))
                .ToList(),
            Conditions = p.Conditions.Select(c => new SlaConditionDto(c.Field, c.Operator, c.Value)).ToList(),
        }).ToList();
    }
}
