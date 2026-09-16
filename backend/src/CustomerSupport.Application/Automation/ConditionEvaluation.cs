using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Automation;

/// <summary>
/// One predicate, normalised from either an <c>SlaPolicyCondition</c> row or a rule's
/// <c>ConditionsJson</c> array — the two storage shapes CS-501 and CS-502/503 use for the same idea.
/// </summary>
public record ConditionSpec(string Field, ConditionOperator Operator, string? Value);

/// <summary>
/// The ticket-shaped data every condition (SLA policy selection, assignment rules, escalation rules)
/// is evaluated against. Callers load these four navigations once and reuse the context across every
/// rule/policy check for one ticket, rather than re-querying per predicate.
/// </summary>
public record TicketEvaluationContext(
    Ticket Ticket,
    TicketCategory Category,
    TicketPriority Priority,
    Customer Customer);

/// <summary>
/// Shared allow-listed condition evaluator (SLA and Automation). Built once here and reused by
/// <c>ISlaEngine</c>'s policy selection (CS-501), <c>IRuleEvaluator</c> (CS-502) and the
/// escalation engine (CS-503) — one field map, so a field either works everywhere or is rejected
/// everywhere, and there is exactly one place to extend it.
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// True when every condition holds (AND). An unknown field evaluates false for that condition
    /// (so the overall result is false) and is reported through <paramref name="onUnknownField"/>
    /// rather than thrown, so a typo in an administrator's rule disables that rule visibly instead
    /// of crashing evaluation for every ticket.
    /// </summary>
    bool Matches(
        IReadOnlyCollection<ConditionSpec> conditions,
        TicketEvaluationContext context,
        Action<string>? onUnknownField = null);
}

/// <summary>
/// The allow-listed field names, exposed separately from <see cref="ConditionEvaluator"/> so the
/// admin rule builders (assignment and escalation rules) can fetch the exact same list the server
/// evaluates against — "the field list fetched from the server so it always matches the allow-list".
/// </summary>
public static class ConditionFields
{
    public static IReadOnlyList<string> All { get; } =
    [
        "CategoryId", "Category.Code", "Priority.Code", "Priority.Level", "Channel",
        "DepartmentId", "Customer.Tier", "Customer.Type", "BranchId", "Subject",
    ];
}

public class ConditionEvaluator(ILogger<ConditionEvaluator> logger) : IConditionEvaluator
{
    /// <summary>
    /// Fixed, allow-listed field paths. Resolved from a dictionary rather than reflection —
    /// reflection over an administrator-supplied field path is both slow and a data-exposure risk.
    /// Field NAMES are shared with <see cref="ConditionFields"/> so the two can never drift.
    /// </summary>
    private static readonly Dictionary<string, Func<TicketEvaluationContext, string?>> Fields = new()
    {
        ["CategoryId"] = c => c.Ticket.CategoryId.ToString(),
        ["Category.Code"] = c => c.Category.Code,
        ["Priority.Code"] = c => c.Priority.Code,
        ["Priority.Level"] = c => c.Priority.Level.ToString(),
        ["Channel"] = c => ((int)c.Ticket.Channel).ToString(),
        ["DepartmentId"] = c => c.Ticket.DepartmentId?.ToString(),
        ["Customer.Tier"] = c => c.Customer.Tier,
        ["Customer.Type"] = c => ((int)c.Customer.Type).ToString(),
        ["BranchId"] = c => c.Ticket.BranchId?.ToString(),
        ["Subject"] = c => c.Ticket.Subject,
    };

    public bool Matches(
        IReadOnlyCollection<ConditionSpec> conditions,
        TicketEvaluationContext context,
        Action<string>? onUnknownField = null)
    {
        foreach (var condition in conditions)
        {
            if (!Fields.TryGetValue(condition.Field, out var resolve))
            {
                logger.LogWarning("Automation condition referenced unknown field {Field}.", condition.Field);
                onUnknownField?.Invoke(condition.Field);
                return false;
            }

            var actual = resolve(context);
            if (!Evaluate(condition.Operator, actual, condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Evaluate(ConditionOperator op, string? actual, string? expected) => op switch
    {
        ConditionOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        ConditionOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        ConditionOperator.In => Split(expected).Contains(actual, StringComparer.OrdinalIgnoreCase),
        ConditionOperator.NotIn => !Split(expected).Contains(actual, StringComparer.OrdinalIgnoreCase),
        ConditionOperator.Contains => actual is not null && expected is not null
            && actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
        ConditionOperator.GreaterThan => CompareNumeric(actual, expected) > 0,
        ConditionOperator.LessThan => CompareNumeric(actual, expected) < 0,
        ConditionOperator.IsNull => string.IsNullOrEmpty(actual),
        ConditionOperator.IsNotNull => !string.IsNullOrEmpty(actual),
        _ => false,
    };

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int CompareNumeric(string? actual, string? expected)
    {
        if (decimal.TryParse(actual, out var a) && decimal.TryParse(expected, out var e))
        {
            return a.CompareTo(e);
        }

        // Falls back to ordinal text comparison so the operator still behaves predictably
        // (rather than throwing) against a non-numeric field an administrator misconfigured.
        return string.CompareOrdinal(actual, expected);
    }
}
