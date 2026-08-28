# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/sla-automation/CS-502-automatic-assignment/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 5 — SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-502-automatic-assignment`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `sla, automation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Automatic assignment
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want tickets routed to the right agent automatically,
So that work starts without someone triaging a queue by hand.

Scope:
- Assignment rules evaluated in order, first match wins.
- Conditions on ticket fields, the customer tier and the channel.
- Strategies: round robin, load balanced, skill based, direct, and queue only.
- Availability and capacity awareness, reusing the manual-assignment rules.
- A rule tester so a manager can see what would happen before enabling a rule.
- A decision log explaining why each ticket was routed as it was.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a ticket is created,
Then active assignment rules are evaluated in order,
And the first whose conditions match is applied,
And evaluation stops there unless the rule says to continue.

Given a round-robin rule,
Then agents are chosen in rotation order,
And the rotation continues correctly across application restarts,
And two tickets arriving simultaneously do not receive the same agent.

Given a load-balanced rule,
Then the eligible agent with the fewest open tickets is chosen.

Given a skill-based rule,
Then only agents with a recorded skill in the ticket category are eligible,
And higher skill levels win ties.

Given every candidate is unavailable or at capacity,
Then the ticket is left in the team queue rather than force-assigned,
And the reason is recorded.

Given no rule matches,
Then the ticket stays unassigned in its department queue.

Given a rule fires,
Then a decision log entry records the rule, the outcome, the chosen agent and the reason.

Given I test a rule against a sample ticket,
Then I see whether it would match and which agent it would choose, without changing anything.

Given automatic assignment, it uses the same availability and capacity rules as manual assignment,
So the two never disagree.
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

- **Blocked by / related ids:** CS-203-assign-tickets-agents
- **Depends on code areas or other stories:**

- CS-203 for `AgentCapacityService`, which this story must reuse rather than reimplement.
- CS-1203 for teams, rotation order and per-member capacity.
- `AssignmentRule` and `AutomationRunLog` entities — already defined, including `Team.RoundRobinCursor`.

## Extra notes (optional)

- The round-robin cursor is shared mutable state under concurrency. It must be advanced under a row lock, or two simultaneous tickets get the same agent.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The decision log is not optional: without it, "why did this ticket go to Ahmed" is unanswerable, and managers stop trusting the automation.

## Out of scope

- What this story explicitly does **not** cover:

- Machine-learning-based routing.
- Reassignment when an agent goes offline mid-shift — an escalation rule concern.
