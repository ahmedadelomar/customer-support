# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-management/CS-102-contact-details/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 1 — Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-102-contact-details`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `customers`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Contact details
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want to manage every way of reaching a customer, not just one email and one phone,
So that I can contact them appropriately and so inbound messages resolve to the right profile.

Scope:
- Multiple contacts per customer, typed as email, mobile, phone, WhatsApp, address, website or other.
- Exactly one primary per type, used as the default target for outbound messages.
- A normalised value on every contact, which is what inbound routing matches against.
- Verification of email and mobile by one-time code.
- A notification opt-out per contact, respected by every outbound channel.
- Address fields for postal contacts.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a customer profile,
When I open the Contact details tab,
Then contacts are grouped by type with the primary shown first.

Given I add a contact,
Then its normalised value is stored (lower-cased for email, digits and a leading plus for phone),
And the value is validated for its type.

Given I add a second contact of a type that already has a primary,
When I mark the new one primary,
Then the previous primary is demoted in the same transaction, so exactly one primary exists per type.

Given I add a contact whose normalised value already belongs to a different customer,
Then I am warned about the conflict before saving, because two customers sharing an address breaks inbound routing.

Given I set a contact as primary,
Then the denormalised PrimaryEmail or PrimaryPhone on the customer is updated to match.

Given I request verification of an email or mobile,
Then a one-time code is sent to that address,
And entering it within the expiry window marks the contact verified with a timestamp.

Given a contact has notifications turned off,
Then no outbound notification is sent to it, though an agent can still reply manually to a ticket.

Given I delete a contact that is the only one of its type and is primary,
Then the customer must retain at least one usable contact overall, or the deletion is refused.
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

- **Blocked by / related ids:** CS-101-customer-profiles
- **Depends on code areas or other stories:**

- CS-101 for the customer profile and the detail page shell.
- `CustomerContact` entity and its `(Type, NormalizedValue)` index — already defined.
- CS-301 and CS-304 for actually sending verification codes; until those land, log the code in development.

## Extra notes (optional)

- The `(Type, NormalizedValue)` index is filtered on `IsDeleted = 0` and is intentionally **not** unique: the same address can legitimately appear on a deleted record. Duplicate detection is a warning, not a database constraint.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The `Contact details` tab already exists on `customer-detail.page.html` and renders grouped contacts read-only. This story adds the mutations.

## Out of scope

- What this story explicitly does **not** cover:

- Address validation against a postal service.
- Bulk contact import.
