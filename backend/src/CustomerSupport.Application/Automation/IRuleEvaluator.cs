using System.Text.Json;
using System.Text.Json.Serialization;
using CustomerSupport.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Automation;

/// <summary>
/// Evaluates the JSON-encoded conditions an <c>AssignmentRule</c> (CS-502) or <c>EscalationRule</c>
/// (CS-503) carries. Thin wrapper: it parses the stored shape and delegates the actual matching to
/// <see cref="IConditionEvaluator"/>, the same allow-listed evaluator CS-501 uses for SLA policy
/// selection — one field map, reused by all three.
/// </summary>
public interface IRuleEvaluator
{
    /// <summary>All conditions must hold (AND). An unknown field evaluates false and is logged.</summary>
    bool Matches(string conditionsJson, TicketEvaluationContext context);
}

public class RuleEvaluator(IConditionEvaluator conditions, ILogger<RuleEvaluator> logger) : IRuleEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record JsonCondition(string Field, string Operator, string? Value);

    public bool Matches(string conditionsJson, TicketEvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson) || conditionsJson.Trim() == "[]")
        {
            return true; // no conditions means the rule always matches
        }

        List<JsonCondition>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<JsonCondition>>(conditionsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Automation rule had malformed conditions JSON; treating as no match.");
            return false;
        }

        if (parsed is null or { Count: 0 })
        {
            return true;
        }

        var specs = new List<ConditionSpec>(parsed.Count);
        foreach (var condition in parsed)
        {
            if (!Enum.TryParse<ConditionOperator>(condition.Operator, ignoreCase: true, out var op))
            {
                logger.LogWarning("Automation rule condition had an unknown operator {Operator}.", condition.Operator);
                return false;
            }

            specs.Add(new ConditionSpec(condition.Field, op, condition.Value));
        }

        return conditions.Matches(specs, context);
    }
}
