using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;

namespace CustomerSupport.Application.Automation.AssignmentRules;

/// <summary>
/// Runs one rule against a real, existing ticket and reports the outcome — without writing
/// anything. The "pick an existing ticket" half of the plan's rule tester; the hypothetical-ticket
/// half is left to a future pass since every verification step in the story exercises a real ticket.
/// </summary>
[RequirePermission(Permissions.Sla.ManageAssignmentRules)]
public record TestAssignmentRuleQuery(Guid RuleId, Guid TicketId) : IRequest<AssignmentPreview>;

public class TestAssignmentRuleQueryHandler(IAssignmentEngine engine) : IRequestHandler<TestAssignmentRuleQuery, AssignmentPreview>
{
    public Task<AssignmentPreview> Handle(TestAssignmentRuleQuery request, CancellationToken cancellationToken) =>
        engine.PreviewRuleAsync(request.TicketId, request.RuleId, cancellationToken);
}
