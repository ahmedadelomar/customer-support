# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/security-administration/CS-1002-permissions/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 10 — Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1002-permissions`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `security, foundation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Permissions
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a system administrator,
I want to see every permission the system defines and control which roles hold which,
So that I can tailor access to how our support organisation actually works without asking for a code change.

Scope:
- A read-only catalogue of permissions, grouped by category, sourced from the code registry.
- A role editor: a matrix of categories against permissions with per-permission toggles.
- Enforcement on both sides: the MediatR AuthorizationBehaviour on the server, and permissionGuard plus the hasPermission directive on the client.
- Changes take effect on the user's next token refresh, and the UI must say so.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open Administration > Roles and select a role,
Then I see every permission grouped by category with a toggle showing whether the role holds it.

Given I grant or revoke a permission,
When I save,
Then a RolePermission row is added or removed,
And an audit entry records who changed what and when.

Given a role is flagged IsSystem,
Then I can edit its permissions but cannot rename or delete it.

Given I revoke a permission from a role,
Then users holding that role lose the ability on their next token refresh,
And the UI warns me that the change applies after their token refreshes rather than instantly.

Given a request carries a RequirePermission attribute the caller does not hold,
Then the server returns 403 with a problem-details body naming the missing permission.

Given a user lacks a permission,
Then the corresponding navigation entry and action button are not rendered,
And navigating to the route directly redirects to /forbidden.

Given the permission list, it always reflects the code registry: adding a permission constant and restarting seeds the new row automatically.
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

- CS-1001 — roles and users must exist first.
- `Permission` and `RolePermission` entities in `backend/src/CustomerSupport.Domain/Identity/`.
- `AuthorizationBehaviour` in `backend/src/CustomerSupport.Application/Common/Behaviours/`.
- `PERMISSIONS` mirror in `frontend/src/app/core/permissions.ts` — must be kept in step with the C# registry.

## Extra notes (optional)

- Permission keys live in exactly one place in code. The seeder upserts them, so the database can never define a permission the code does not know.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The client mirror and the server registry are two files that must agree. Add a test that asserts the two lists match, so drift fails the build rather than production.

## Out of scope

- What this story explicitly does **not** cover:

- Per-record and per-field permissions.
- Custom roles scoped to a single branch — the column exists but the UI for it is deferred.
- Delegated administration.
