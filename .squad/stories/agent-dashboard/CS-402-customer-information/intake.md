# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/agent-dashboard/CS-402-customer-information/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 4 — Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-402-customer-information`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `workspace`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Customer information
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent working a ticket,
I want the customer's context beside the conversation,
So that I do not have to open another screen to know who I am helping.

Scope:
- A customer panel on the ticket screen: identity, tier, contacts, preferred language and channel.
- Their other open tickets, with a warning when several are open at once.
- Pinned notes surfaced inline.
- Satisfaction score and last interaction.
- Quick actions: call, email, open the full profile.
- Inline editing of the fields agents correct most often, without leaving the ticket.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open a ticket,
Then the customer panel shows the display name in the active language, code, type, tier and status.

Given the customer is blocked,
Then the panel shows a prominent warning with the reason.

Given the customer has other open tickets,
Then they are listed with number, subject and status,
And I can open one in a new tab without losing my place.

Given the customer has pinned notes,
Then they appear in the panel without needing to open the profile.

Given a contact in the panel,
Then I can click to email or call it,
And contacts with notifications disabled are visibly marked.

Given I correct the customer's name or preferred language inline,
Then it saves without navigating away,
And the change is audited.

Given the panel on a smaller viewport,
Then it collapses into an expandable section above the conversation rather than being hidden entirely.

Given I lack customers.view,
Then the panel shows only the display name and nothing else.
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

- CS-101 for the profile, CS-102 for contacts, CS-104 for pinned notes.
- CS-201 for the ticket detail screen the panel lives in.

## Extra notes (optional)

- Several open tickets from one customer usually means a duplicate, so surfacing them here is also duplicate prevention.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Fetch the panel with the ticket in one request rather than a second round trip; the ticket detail DTO already carries a customer summary.

## Out of scope

- What this story explicitly does **not** cover:

- Full profile editing — that stays on the customer screen.
- Merging duplicate customers.
