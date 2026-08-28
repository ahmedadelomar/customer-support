# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/sla-automation/CS-501-response-resolution-targets/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 5 — SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-501-response-resolution-targets`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `sla, core`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Response and resolution targets
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want response and resolution commitments measured against working hours,
So that our promises to customers are tracked accurately and breaches are visible before they happen.

Scope:
- SLA policies with per-priority first-response and resolution targets.
- Business calendars: working hours, holidays and time zones, so a 4-hour target means 4 working hours.
- Policy selection by conditions, with a default fallback.
- Live SLA clocks per ticket, one per commitment.
- Pausing while waiting on the customer, and resuming without losing elapsed time.
- Breach detection and an approaching-breach warning.
- SLA state visible on the ticket, the list and the dashboard.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a ticket is created,
Then the first matching SLA policy is applied by evaluation order,
And two clocks are created, one for first response and one for resolution,
And their due times are computed in working minutes against the policy calendar.

Given a target of 4 hours and a calendar of 08:00 to 17:00 Sunday to Thursday,
When a ticket arrives at 16:00 on Thursday,
Then the due time is 11:00 on the following Sunday, not 20:00 on Thursday.

Given a holiday falls between now and the computed due time,
Then it is excluded from the calculation.

Given an agent replies for the first time,
Then the first-response clock stops as met,
And the resolution clock continues.

Given the ticket moves to a status whose kind pauses SLA,
Then the resolution clock pauses and accumulates paused minutes,
And the first-response clock is unaffected.

Given the ticket returns to an active status,
Then the resolution clock resumes and its due time is recomputed from the remaining budget,
Not restarted from the beginning.

Given elapsed working time passes the warning threshold,
Then a warning is raised once, not repeatedly.

Given the due time passes without the commitment being met,
Then the clock is marked breached, the breach time recorded, and the denormalised flag on the ticket set.

Given a ticket is resolved,
Then the resolution clock stops as met or breached according to whether it passed its due time.

Given the ticket list,
Then I can filter by SLA state and sort by remaining time.
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

- **Blocked by / related ids:** CS-201-create-track-tickets
- **Depends on code areas or other stories:**

- CS-201 for tickets, CS-202 for priorities, CS-204 for the status kinds that pause and stop clocks.
- `SlaPolicy`, `SlaTarget`, `SlaPolicyCondition`, `BusinessCalendar`, `BusinessHour`, `Holiday`, `TicketSlaClock` — all already defined.
- `ISlaEngine` and `IBusinessCalendarCalculator` — declared, not implemented.
- `DbSeeder` already seeds a default calendar (Sunday–Thursday 08:00–17:00, Asia/Riyadh) and a Standard SLA policy.

## Extra notes (optional)

- Working-hours arithmetic is the hardest correctness problem in this product. It deserves an exhaustive unit-test suite, including holidays, split shifts, daylight-saving transitions and targets spanning weekends.
- Resuming rather than restarting after a pause is essential: restarting would erase existing breaches from reports.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- `TicketSlaClock` is the authority; the columns on `Ticket` mirror it so list queries avoid a join. This story owns keeping them in sync.

## Out of scope

- What this story explicitly does **not** cover:

- Escalation actions on breach — CS-503.
- SLA performance reporting — CS-902.
