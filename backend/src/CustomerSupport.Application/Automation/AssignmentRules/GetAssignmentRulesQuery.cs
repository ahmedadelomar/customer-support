using System.Text.Json;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation.AssignmentRules;

[RequirePermission(Permissions.Sla.ManageAssignmentRules)]
public record GetAssignmentRulesQuery : IRequest<IReadOnlyList<AssignmentRuleDto>>;

public class GetAssignmentRulesQueryHandler(IAppDbContext db) : IRequestHandler<GetAssignmentRulesQuery, IReadOnlyList<AssignmentRuleDto>>
{
    public async Task<IReadOnlyList<AssignmentRuleDto>> Handle(GetAssignmentRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await db.AssignmentRules.AsNoTracking()
            .OrderBy(r => r.EvaluationOrder).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return rules.Select(AssignmentRuleMapping.ToDto).ToList();
    }
}

internal static class AssignmentRuleMapping
{
    public static AssignmentRuleDto ToDto(Domain.Automation.AssignmentRule r) => new()
    {
        Id = r.Id,
        NameEn = r.Name.En,
        NameAr = r.Name.Ar,
        Description = r.Description,
        EvaluationOrder = r.EvaluationOrder,
        IsActive = r.IsActive,
        Conditions = ConditionJson.Parse(r.ConditionsJson),
        Strategy = r.Strategy,
        TargetDepartmentId = r.TargetDepartmentId,
        TargetTeamId = r.TargetTeamId,
        TargetUserId = r.TargetUserId,
        RespectAgentAvailability = r.RespectAgentAvailability,
        StopProcessing = r.StopProcessing,
        MatchCount = r.MatchCount,
        LastMatchedAt = r.LastMatchedAt,
    };
}

/// <summary>Shared JSON shape read/written by both assignment and escalation rule admin endpoints.</summary>
internal static class ConditionJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private record Raw(string Field, string Operator, string? Value);

    public static List<AssignmentRuleConditionInput> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<Raw>>(json, Options) ?? [];
            return raw
                .Where(r => Enum.TryParse<Domain.Enums.ConditionOperator>(r.Operator, true, out _))
                .Select(r => new AssignmentRuleConditionInput(r.Field, Enum.Parse<Domain.Enums.ConditionOperator>(r.Operator, true), r.Value))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Write(IEnumerable<AssignmentRuleConditionInput> conditions) =>
        JsonSerializer.Serialize(conditions.Select(c => new Raw(c.Field, c.Operator.ToString(), c.Value)));
}
