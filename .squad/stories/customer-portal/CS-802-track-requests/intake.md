# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-portal/CS-802-track-requests/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 8 — Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-802-track-requests`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `portal, self-service`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Track requests
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to see what is happening with my open requests,
So that I stop having to ask for updates.

Scope:
- A list of my open and recent tickets with status.
- A ticket detail view showing the conversation, excluding anything internal.
- Replying to a ticket from the portal.
- Attaching files to a reply.
- A visible expectation of when I will hear back.
- Closing my own request when it is no longer needed.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am signed in,
When I open My requests,
Then I see my tickets with number, subject, status, last update and channel,
And they are filterable by open or closed.

Given I open a ticket,
Then I see the conversation in chronological order,
And internal notes, mentions and agent-only events are absent.

Given the status,
Then it is shown using the customer-facing label, and statuses marked not visible in the portal are shown as a neutral In progress.

Given I reply,
Then my message is added to the ticket,
And the assigned agent is notified,
And the ticket reopens if it was resolved.

Given I attach a file to a reply,
Then the same size and type limits apply as elsewhere,
And I can download attachments the agent has shared with me but not internal ones.

Given a ticket with a response commitment,
Then I see an indication of when to expect a reply,
Without exposing internal SLA mechanics or breach status.

Given I close my own ticket,
Then it moves to a closed status with a system event recording that the customer closed it.

Given a ticket belonging to another customer,
Then requesting it by id returns not found.
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

- **Blocked by / related ids:** CS-801-submit-tickets
- **Depends on code areas or other stories:**

- CS-801 for portal accounts and the shell.
- CS-201 for tickets and messages, CS-204 for status kinds and reopening.
- CS-501 for the response expectation shown to the customer.

## Extra notes (optional)

- The rule that no internal content ever reaches the portal is the most important thing in this story, and the easiest to get wrong by adding a field to a shared DTO.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Use portal-specific DTOs rather than reusing the agent ones with fields stripped. A shared DTO leaks the next field someone adds.

## Out of scope

- What this story explicitly does **not** cover:

- Live chat from the portal — CS-303.
- Editing a submitted ticket.
