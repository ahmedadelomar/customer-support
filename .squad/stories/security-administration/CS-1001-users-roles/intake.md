# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/security-administration/CS-1001-users-roles/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 10 — Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1001-users-roles`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `security, foundation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Users and roles
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a system administrator,
I want to create and manage agent and administrator accounts and assign them to roles,
So that only authorised staff can access the CRM and each person sees only what their job requires.

This is the first story in the whole product: nothing else can be secured until identities exist.

Scope:
- Sign-in with username and password, issuing a JWT access token and a refresh token.
- User CRUD: create, edit, deactivate and reactivate. Users are never hard-deleted, because tickets and audit rows reference them.
- Assign a user to one or more roles, a home branch, and a department.
- Ship the six system roles: System Administrator, Support Manager, Team Leader, Agent, Knowledge Author, Viewer.
- Password policy, account lockout after repeated failures, and a forced password change for admin-created accounts.
- Agent availability status (Available, Busy, Away, Offline) and a concurrent-ticket cap, because automatic assignment reads both.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am a system administrator,
When I open Administration > Users,
Then I see a paged, searchable list showing display name, username, email, roles, department, branch and status.

Given I am creating a user,
When I submit the form without a role,
Then the request is rejected with a field-level error, because a user with no role can sign in but do nothing.

Given I create a user,
Then the account is created with MustChangePassword set,
And the user is forced to set a new password before reaching any other screen.

Given a user signs in successfully,
Then the access token carries their user id, branch, accessible branches, department, roles and one perm claim per granted permission,
And LastLoginAt and LastLoginIp are recorded.

Given a user enters the wrong password 5 times,
Then the account is locked for 15 minutes,
And the response does not reveal whether the username exists.

Given I deactivate a user,
Then they cannot sign in,
And their existing tickets and audit history remain intact and still show their name.

Given I am not a system administrator,
When I call any user management endpoint,
Then I receive 403.

Given the portal, all labels and validation messages render in the active language (Arabic or English).
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

- **Blocked by / related ids:** —
- **Depends on code areas or other stories:**

- ASP.NET Identity tables (`ApplicationUser`, `ApplicationRole`) — already defined in `backend/src/CustomerSupport.Infrastructure/Identity/`.
- `Permissions` registry in `backend/src/CustomerSupport.Application/Common/Security/Permissions.cs`.
- `DbSeeder` already seeds the permission rows and the six system roles.
- Angular `AuthService`, `authGuard` and `permissionGuard` scaffolding in `frontend/src/app/core/auth/`.

## Extra notes (optional)

- The seeder creates a bootstrap administrator only when `SEED_ADMIN_PASSWORD` is set. Document this in the deployment runbook.
- Refresh tokens must be stored hashed and be single-use, so a stolen token cannot be replayed after rotation.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The `/api/auth/login` response shape is already typed on the client as `AuthResult` in `frontend/src/app/core/auth/auth.models.ts` — implement the server to match it.
- Permission claims use the claim type `perm`, read by `CurrentUserService`.

## Out of scope

- What this story explicitly does **not** cover:

- Single sign-on, SAML and OIDC federation — covered by a later Integrations story.
- Two-factor authentication.
- Customer-portal account registration — that is CS-801.
- Editing which permissions a role holds — that is CS-1002.
