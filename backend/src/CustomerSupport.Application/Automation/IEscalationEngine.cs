namespace CustomerSupport.Application.Automation;

/// <summary>One ticket the tester found matching a rule's trigger and conditions, with why.</summary>
public record EscalationTestMatch(Guid TicketId, string TicketNumber, string Reason);

/// <summary>
/// Evaluates escalation rules on a schedule (SLA and Automation / Escalation rules). Kept as its own
/// interface — separate from the Quartz job that calls it every 5 minutes — so it can run in a unit
/// test without a scheduler, and so the rule tester can call the read-only half without firing anything.
/// </summary>
public interface IEscalationEngine
{
    /// <summary>Evaluates every active rule against its trigger's candidates and fires matching actions. Returns the number of rules that fired at least once.</summary>
    Task<int> EvaluateAsync(CancellationToken ct = default);

    /// <summary>Dry-run: the currently open tickets one rule would fire on right now, with the reason. Fires nothing.</summary>
    Task<IReadOnlyList<EscalationTestMatch>> TestRuleAsync(Guid ruleId, CancellationToken ct = default);
}
