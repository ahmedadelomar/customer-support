# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/platform/CS-1204-multi-branch/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 12 — Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1204-multi-branch`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `platform, foundation, security`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Multi-branch
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an organisation operating in several locations,
I want data scoped by branch with controlled cross-branch access,
So that each branch works with its own customers and tickets while head office can see everything.

Scope:
- Branch CRUD, each with its own time zone.
- Branch scoping applied to every tenant-aware query, driven by the user's home branch plus any additional accessible branches.
- A branch switcher for users with access to more than one.
- Head-office users with unrestricted access.
- Branch-scoped configuration and reporting.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an administrator,
Then I can create and edit branches, each with a code, bilingual name, time zone, address and phone.

Given I am a user assigned to one branch,
When I list customers or tickets,
Then I see only records in my branch plus records that are global (null branch).

Given I have access to more than one branch,
Then a branch switcher appears in the top bar,
And switching changes what I see and which branch new records are created in.

Given I am a head-office user with no branch restrictions,
Then I see records across every branch,
And reports can group by branch.

Given a record is created,
Then its branch is set from my currently active branch.

Given a background job or a cross-branch report,
Then it can deliberately read across all branches, because scoping is opt-in per query rather than a global filter.

Given I try to open a record in a branch I cannot access,
Then I receive 404 rather than 403, so the existence of the record is not disclosed.

Given a branch still has active users or open tickets,
When I try to deactivate it,
Then the request is refused.
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

- `Branch` entity and the `ITenantScoped` interface — already defined.
- `WhereBranchAccessible` extension in `backend/src/CustomerSupport.Application/Common/Interfaces/QueryableExtensions.ts` — already implemented.
- `ApplicationUser.BranchId` and `AccessibleBranchIds`, and the `branch` / `branches` JWT claims — already emitted by CS-1001.
- CS-1004 for branch-scoped settings resolution.

## Extra notes (optional)

- Branch scoping is deliberately not an EF global query filter. A filter would silently hide rows from background jobs and cross-branch reports, which both legitimately need the whole table. The trade-off is that every handler must remember `WhereBranchAccessible` — so this story adds a test that catches handlers which forget.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Return 404 rather than 403 for out-of-scope records: a 403 confirms the record exists.

## Out of scope

- What this story explicitly does **not** cover:

- Full database-per-tenant isolation.
- Branch-level data residency.
