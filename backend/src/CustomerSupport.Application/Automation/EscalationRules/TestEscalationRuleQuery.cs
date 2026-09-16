using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Security;
using MediatR;

namespace CustomerSupport.Application.Automation.EscalationRules;

/// <summary>Dry-run: the currently open tickets this rule would fire on right now, with why. Fires nothing.</summary>
[RequirePermission(Permissions.Sla.ManageEscalationRules)]
public record TestEscalationRuleQuery(Guid RuleId) : IRequest<IReadOnlyList<EscalationTestMatch>>;

public class TestEscalationRuleQueryHandler(IEscalationEngine engine)
    : IRequestHandler<TestEscalationRuleQuery, IReadOnlyList<EscalationTestMatch>>
{
    public Task<IReadOnlyList<EscalationTestMatch>> Handle(TestEscalationRuleQuery request, CancellationToken cancellationToken) =>
        engine.TestRuleAsync(request.RuleId, cancellationToken);
}
