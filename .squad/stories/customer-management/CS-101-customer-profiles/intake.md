# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-management/CS-101-customer-profiles/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 1 — Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-101-customer-profiles`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `customers, reference-slice`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Customer profiles
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want a single profile per customer with their identity, preferences and support summary,
So that I know who I am talking to without piecing it together from separate tickets.

Scope:
- Customer CRUD with a generated reference code, bilingual display name, and individual/company/government types.
- Preferred language and preferred channel, which drive every outbound message.
- Service tier and account manager.
- Blocking a customer, with a required reason, to stop new inbound tickets without losing history.
- Summary counts on the profile header: total tickets, open tickets, notes, satisfaction score.
- A paged, filterable, sortable list with search across name, code, email, phone and national ID.
- Duplicate prevention on email and phone, because split profiles split the interaction history.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open Customers,
Then I see a paged list with code, name, type, email, phone, open ticket count, status and last interaction,
And I can search by name, code, email, phone or national ID,
And I can filter by type, status and whether the customer has open tickets.

Given I create a customer,
Then a unique code is generated in the form CUS-000123,
And primary email and phone contacts are created alongside the profile in the same request.

Given I create a customer with an email that already belongs to another customer,
Then the request is refused with 409 and a message naming the conflict.

Given I create a customer with neither an email nor a phone,
Then the request is refused, because an unreachable customer cannot be supported.

Given a company or government customer,
Then a company name is required.

Given I block a customer,
Then a reason is required,
And the reason is shown on the profile,
And new inbound tickets from that customer are refused.

Given I delete a customer who has open tickets,
Then the request is refused with a message stating how many remain.

Given I delete a customer with no open tickets,
Then the record is soft-deleted and disappears from lists while history is retained.

Given the list, all filter state is reflected in the URL so a filtered view can be shared and survives a refresh.
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

- **Blocked by / related ids:** CS-1001-users-roles
- **Depends on code areas or other stories:**

- CS-1001 for authentication and the `customers.*` permissions.
- CS-1204 for branch scoping on the list query.
- `Customer` entity, `CustomersService`, `CustomerListPage`, `CustomerFormPage` — **already implemented in the skeleton** as the reference vertical slice.

## Extra notes (optional)

- This slice is already built end to end and serves as the pattern every other feature copies. The story is to complete it (branch scoping, export, the interaction-history hook) and to treat it as the reference.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- `ReferenceNumberGenerator` allocates codes from a SQL sequence rather than counting rows, so concurrent creates cannot collide.

## Out of scope

- What this story explicitly does **not** cover:

- Merging duplicate customers.
- Customer portal self-registration — that is CS-801.
- Contact management beyond the primary pair — that is CS-102.
