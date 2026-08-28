# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-portal/CS-801-submit-tickets/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 8 — Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-801-submit-tickets`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `portal, self-service`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Submit tickets
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to sign in to a portal and raise a support request myself,
So that I can get help without waiting on a phone line or composing an email.

Scope:
- Customer portal accounts: registration, email verification, sign-in, password reset.
- A guided ticket submission form with only the categories customers may choose.
- File attachments on submission.
- Immediate acknowledgement with the ticket number.
- Linking a portal account to an existing customer profile rather than duplicating it.
- A portal shell distinct from the agent workspace, branded per branch.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I register with an email that already belongs to a customer profile,
Then my account links to that existing profile rather than creating a duplicate,
And I must verify the email before I can sign in.

Given I register with an unknown email,
Then a customer profile is created and linked to my account.

Given I request a password reset,
Then the response is identical whether or not the account exists,
So the endpoint cannot be used to discover who has an account.

Given I am signed in,
When I submit a ticket,
Then I choose from portal-visible categories only,
And I provide a subject and description,
And I can attach files within the configured limits.

Given I submit,
Then a ticket is created on the Portal channel with my customer profile,
And I see the ticket number immediately,
And I receive an acknowledgement in my preferred language.

Given my customer profile is blocked,
Then I cannot submit a ticket and I am told to contact support by another means.

Given the portal,
Then it uses the branch branding and is fully bilingual with correct direction.

Given I am not signed in,
Then I can still browse public FAQs, but not submit or view tickets.
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

- CS-1001 for the Identity model — `ApplicationUser.UserType` and `CustomerId` already exist for exactly this.
- CS-101 for customer profiles, CS-201 for ticket creation, CS-104 for attachments.
- CS-1205 for branding, CS-1201 for bilingual support.

## Extra notes (optional)

- Portal accounts reuse the same Identity table as agents, distinguished by `UserType`. One authentication system, one lockout policy, one password rule.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The portal is a separate route area with its own shell, but the same Angular application and the same API.

## Out of scope

- What this story explicitly does **not** cover:

- Social sign-in.
- Anonymous ticket submission — that is the web form channel, CS-305.
- Portal user management by agents.
