# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/communication-channels/CS-305-web-forms/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 3 — Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-305-web-forms`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `channels, forms`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Web forms
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an organisation,
I want configurable public forms that create tickets,
So that we can capture structured requests from our website without a developer changing code.

Scope:
- Form definitions with a JSON field schema, so a new form needs no schema change.
- A public submission endpoint with captcha and rate limiting.
- Ticket creation from a submission, mapping fields to ticket properties.
- Raw submissions retained even when ticket creation fails, so nothing is lost.
- An embeddable snippet and a hosted form page.
- Bilingual labels and validation.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an administrator,
When I create a form,
Then I can define fields with a key, type, bilingual label, required flag, options and validation,
And I can set the default category, priority and department for tickets it creates.

Given a form is active,
Then it is reachable at a public URL by its key,
And an embed snippet is provided.

Given a visitor submits a valid form,
Then the raw payload is stored,
And a ticket is created with the mapped properties,
And the visitor sees the configured thank-you message.

Given the submission is missing a required field or fails validation,
Then it is rejected with per-field messages in the submission language.

Given captcha is enabled and the token is missing or invalid,
Then the submission is rejected.

Given more submissions arrive from one IP than the configured hourly limit,
Then further submissions are rejected.

Given ticket creation fails after the payload was stored,
Then the submission is marked failed with a reason,
And a retry job can process it later without the visitor resubmitting.

Given a submitter email matches an existing customer,
Then the ticket is linked to that customer rather than creating a duplicate.
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

- `WebFormDefinition` and `WebFormSubmission` entities — already defined, including the rate-limit index on `(IpAddress, SubmittedAt)`.
- CS-201 for ticket creation, CS-101 for customer matching.

## Extra notes (optional)

- Storing the payload before attempting ticket creation is what makes the endpoint safe: a downstream failure never loses a customer request.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The public endpoint is anonymous and therefore the most exposed surface in the product. Captcha, rate limiting and strict validation are all load-bearing.

## Out of scope

- What this story explicitly does **not** cover:

- A drag-and-drop visual form builder — a JSON schema editor is sufficient for this story.
- Conditional field logic.
- File uploads on public forms, which would need separate abuse controls.
