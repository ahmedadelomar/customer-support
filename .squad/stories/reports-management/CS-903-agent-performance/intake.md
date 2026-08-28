# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/reports-management/CS-903-agent-performance/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 9 — Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-903-agent-performance`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `reports`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Agent performance
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a team leader,
I want visibility of how each agent is doing,
So that I can support the ones who are struggling and recognise the ones who are not.

Scope:
- Per-agent volume, resolution, response time, SLA compliance and satisfaction.
- Team comparison and rankings.
- Workload distribution, to identify uneven assignment.
- Activity over time.
- Quality indicators: reopen rate, escalation rate, AI suggestion acceptance.
- Access limited so agents see themselves and leaders see their teams.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given the agent performance report,
Then I see per agent: assigned, resolved, average first response, average resolution, SLA compliance, satisfaction and reopen rate.

Given I am an agent without the agent-performance permission,
Then I see only my own figures.

Given I am a team leader,
Then I see my team members; a manager sees the whole department.

Given workload distribution,
Then I see open tickets per agent against their capacity,
So uneven assignment is visible rather than inferred.

Given rankings,
Then they are shown with the underlying counts,
And metrics based on very few tickets are marked as low-confidence rather than presented as comparable.

Given an agent who was away,
Then their figures reflect the period they actually worked, rather than making absence look like poor performance.

Given quality indicators,
Then reopen rate and escalation rate are shown alongside volume,
Because volume alone rewards closing tickets rather than solving problems.

Given I drill into a figure,
Then I see the tickets behind it.
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

- **Blocked by / related ids:** CS-901-ticket-reports
- **Depends on code areas or other stories:**

- CS-901 for the aggregation infrastructure.
- CS-805 for satisfaction scores.
- CS-701 for AI acceptance rates.
- CS-1001 for the permissions that scope visibility.

## Extra notes (optional)

- Presenting volume without quality indicators drives the wrong behaviour. Both must appear together.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Mark metrics computed from a small sample as low-confidence in the response, so the UI can render them differently rather than the client guessing.

## Out of scope

- What this story explicitly does **not** cover:

- Automated performance scoring or ratings.
- Integration with HR systems.
