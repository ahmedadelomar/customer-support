using CustomerSupport.Application.Automation;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Runs escalation rule evaluation every 5 minutes (SLA and Automation / Escalation rules). All of
/// the actual work — candidates, cooldowns, actions, logging — lives in <see cref="IEscalationEngine"/>
/// so it can be exercised in a unit test without a scheduler; this job is just the trigger.
/// </summary>
[DisallowConcurrentExecution]
public class EscalationEvaluationJob(IEscalationEngine engine, ILogger<EscalationEvaluationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var rulesFired = await engine.EvaluateAsync(context.CancellationToken);
        if (rulesFired > 0)
        {
            logger.LogInformation("Escalation pass complete: {RulesFired} rule(s) fired at least once.", rulesFired);
        }
    }
}
