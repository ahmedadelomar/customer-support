# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/sla-automation/CS-503-escalation-rules/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 5 — SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-503-escalation-rules`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `sla, automation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Escalation rules
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want tickets at risk to escalate automatically,
So that problems surface before a customer complains rather than after.

Scope:
- Rules triggered by approaching breach, actual breach, agent silence, repeated customer replies or repeated reopens.
- Actions: notify a manager, reassign, raise priority, change department, increase escalation level, add a watcher.
- Cooldown and maximum fires per ticket so a rule cannot spam.
- Evaluation by a background job, so escalation happens whether or not anyone is looking.
- A decision log and a rule tester.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given an active escalation rule with an approaching-breach trigger at 80 percent,
When a ticket's elapsed working time crosses 80 percent of its target,
Then the rule fires once and its action is applied.

Given the same rule and the same ticket within the cooldown window,
Then it does not fire again.

Given a rule with a maximum of 3 fires per ticket,
Then it stops firing after the third, and the ticket is not escalated further by that rule.

Given a rule with a no-agent-response trigger of 120 minutes,
When no agent has replied for that long during working hours,
Then it fires.

Given a raise-priority action,
Then the ticket moves one priority level up,
And if already at the highest, the action is skipped and the reason recorded.

Given a notify-manager action,
Then the department manager and any specified role holders are notified,
And they are added as watchers.

Given a rule fires,
Then a ticket history event records it, attributed to the rule by name rather than to a person.

Given the evaluation job runs,
Then it processes tickets in batches and completes within its interval,
And an overrun does not cause overlapping runs.

Given I test a rule,
Then I see which currently open tickets it would fire on, without firing it.

Given a rule is deactivated,
Then it stops firing immediately, and its history remains.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| — | None. |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CS-501-response-resolution-targets
- **Depends on code areas or other stories:**

- CS-501 for the SLA clocks the triggers read.
- CS-502 for the rule-evaluation and decision-log patterns to reuse.
- `EscalationRule` and `AutomationRunLog` entities — already defined, including cooldown and max-fire columns.
- CS-504 for notification delivery.

## Extra notes (optional)

- Escalation runs on a timer, not on request. A rule that only fires when someone opens a screen is not an escalation rule.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Cooldown and max-fire checks read `AutomationRunLog` using the `(TicketId, RuleId, OccurredAt)` index.

## Out of scope

- What this story explicitly does **not** cover:

- Approval workflows.
- Customer-facing escalation notifications.
