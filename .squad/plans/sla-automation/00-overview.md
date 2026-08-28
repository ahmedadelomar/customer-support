# sla-automation — plan overview

Entry point for the **Section 5 — SLA & Automation** feature. Stories execute in order by their `NN` prefix.

The machinery that makes support promises measurable and routing automatic. Story 29 is the hardest
correctness problem in the product — working-hours arithmetic — and everything else in this feature reads its
output. Stories 30 and 31 share a rule-evaluation pattern; story 32 is the delivery layer almost every other
feature already depends on.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 29 | [29-story-response-resolution-targets-CS-501-response-resolution-targets.md](29-story-response-resolution-targets-CS-501-response-resolution-targets.md) | SLA policies, business calendars and live clocks | CS-501-response-resolution-targets | 17 |
| 30 | [30-story-automatic-assignment-CS-502-automatic-assignment.md](30-story-automatic-assignment-CS-502-automatic-assignment.md) | Assignment rules and routing strategies | CS-502-automatic-assignment | 16 |
| 31 | [31-story-escalation-rules-CS-503-escalation-rules.md](31-story-escalation-rules-CS-503-escalation-rules.md) | Time-driven escalation rules | CS-503-escalation-rules | 29 |
| 32 | [32-story-alerts-notifications-CS-504-alerts-notifications.md](32-story-alerts-notifications-CS-504-alerts-notifications.md) | Notification centre, preferences and reliable fan-out | CS-504-alerts-notifications | 31 |

## Dependency notes

- **Story 29 must land before 31.** Escalation triggers read SLA clocks that only exist once 29 is built.
- **Story 30 must reuse `AgentCapacityService` from CS-203.** Two capacity implementations would let manual and automatic assignment disagree, which agents experience as the system being broken.
- **Story 32 (notifications) is a dependency of CS-403, CS-405, CS-503 and Section 9.** Those stories build against `INotificationDispatcher`; this story puts real channels behind it. Consider pulling it forward if those are blocked.
- Stories 30 and 31 share condition evaluation, the decision log and the rule tester. Build the shared `IRuleEvaluator` in 30 and reuse it in 31 rather than writing it twice.
- CS-204 owns status kinds; CS-501 owns the clocks those kinds pause. Agree the pause/resume contract between the two before either merges.
- `ISlaEngine`, `IBusinessCalendarCalculator`, `IAssignmentEngine` and `INotificationDispatcher` are already declared in `IServiceAbstractions.cs` and are called from CS-201 and CS-204. This feature implements them.
