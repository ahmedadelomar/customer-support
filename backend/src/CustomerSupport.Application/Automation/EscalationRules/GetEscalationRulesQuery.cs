using CustomerSupport.Application.Automation.AssignmentRules;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation.EscalationRules;

[RequirePermission(Permissions.Sla.ManageEscalationRules)]
public record GetEscalationRulesQuery : IRequest<IReadOnlyList<EscalationRuleDto>>;

public class GetEscalationRulesQueryHandler(IAppDbContext db) : IRequestHandler<GetEscalationRulesQuery, IReadOnlyList<EscalationRuleDto>>
{
    public async Task<IReadOnlyList<EscalationRuleDto>> Handle(GetEscalationRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await db.EscalationRules.AsNoTracking()
            .OrderBy(r => r.EvaluationOrder).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return rules.Select(r => new EscalationRuleDto
        {
            Id = r.Id,
            NameEn = r.Name.En,
            NameAr = r.Name.Ar,
            Description = r.Description,
            EvaluationOrder = r.EvaluationOrder,
            IsActive = r.IsActive,
            Trigger = r.Trigger,
            ThresholdPercent = r.ThresholdPercent,
            ThresholdMinutes = r.ThresholdMinutes,
            ThresholdCount = r.ThresholdCount,
            TargetType = r.TargetType,
            Conditions = ConditionJson.Parse(r.ConditionsJson),
            Action = r.Action,
            ActionTargetUserId = r.ActionTargetUserId,
            ActionTargetTeamId = r.ActionTargetTeamId,
            ActionTargetDepartmentId = r.ActionTargetDepartmentId,
            ActionTargetPriorityId = r.ActionTargetPriorityId,
            ActionNotifyRoleId = r.ActionNotifyRoleId,
            CooldownMinutes = r.CooldownMinutes,
            MaxFiresPerTicket = r.MaxFiresPerTicket,
            FireCount = r.FireCount,
            LastFiredAt = r.LastFiredAt,
        }).ToList();
    }
}
