# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-portal/CS-803-view-history/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 8 — Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-803-view-history`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `portal, self-service`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
View history
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to see my past requests and interactions,
So that I can refer back to what was agreed without asking again.

Scope:
- Full history of closed and resolved tickets.
- Searching and filtering my own history.
- Viewing a closed conversation read-only.
- Downloading a ticket summary.
- Reopening a recently closed request instead of raising a duplicate.
- Managing my own profile details and contact preferences.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open History,
Then I see all my past tickets with number, subject, category, resolution date and satisfaction score,
Paged and searchable by number, subject and content.

Given I filter by date range, status or category,
Then results narrow accordingly.

Given I open a closed ticket,
Then I can read the full customer-visible conversation,
And the reply box is disabled with an explanation.

Given a ticket closed within the reopen window,
Then I can reopen it with a reason instead of creating a duplicate,
And it returns to an open status with the history intact.

Given a ticket closed beyond that window,
Then reopening is offered as creating a linked follow-up request instead.

Given I download a ticket summary,
Then I receive a document containing the conversation, dates and resolution, in my language.

Given my profile page,
Then I can update my name, preferred language, preferred channel and contact details,
And changes are audited like any other customer change.

Given a contact change to an email or phone,
Then it requires verification before it becomes primary.
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

- **Blocked by / related ids:** CS-802-track-requests
- **Depends on code areas or other stories:**

- CS-802 for the ticket views this extends.
- CS-102 for contact management and verification.
- CS-103 for the interaction timeline the history draws on.

## Extra notes (optional)

- Offering a reopen instead of a new ticket meaningfully reduces duplicates, which are the main source of split history.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The reopen window should reuse `tickets.autoCloseResolvedAfterDays` semantics rather than inventing a second setting.

## Out of scope

- What this story explicitly does **not** cover:

- Bulk export of all customer data.
- Deleting history.
