# Story 02 — Permission catalogue and role editor (Story: CS-1002-permissions)

## Prerequisites

- Story 01 must be complete: roles, users and the token pipeline must exist.
- The permission rows are already seeded from the code registry by `DbSeeder.SeedPermissionsAsync`. This story does not change how they are created.

## Story Goal

An administrator can see every permission the system defines, grouped by category, and toggle which
roles hold which. Changes are audited and take effect at the user's next token refresh — and the UI says so
plainly, because a silent delay looks like a bug.

This story also closes the drift risk between the two copies of the permission list by adding a test that
fails when `Permissions.cs` and `permissions.ts` disagree.

## Context — Read These Files First

1. `.squad/stories/security-administration/CS-1002-permissions/intake.md`.
2. [backend/src/CustomerSupport.Application/Common/Security/Permissions.cs](backend/src/CustomerSupport.Application/Common/Security/Permissions.cs) — `Permissions.All` already discovers every key by reflection.
3. [backend/src/CustomerSupport.Application/Common/Behaviours/AuthorizationBehaviour.cs](backend/src/CustomerSupport.Application/Common/Behaviours/AuthorizationBehaviour.cs) — enforcement is already wired; this story only makes the grants editable.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs) — note that seeding **only adds missing grants**, so manual revocations survive a restart. Do not change that.
5. [frontend/src/app/core/permissions.ts](frontend/src/app/core/permissions.ts) — the client mirror.
6. [frontend/src/app/core/directives/has-permission.directive.ts](frontend/src/app/core/directives/has-permission.directive.ts) — already implemented, including the `any` mode.

## Product rules (from story)

- **Permissions are defined in code, never in the UI.** The catalogue is read-only; only the grants are editable.
- **System roles cannot be renamed or deleted**, but their permissions can be edited — an organisation may legitimately want a narrower Agent role.
- **A grant change is audited** with the role, the permission key and the actor.
- **Changes apply at the next token refresh**, not immediately. Show this as an inline notice on the role editor.
- **Revoking `admin.roles.manage` from your own only role is refused**, otherwise an administrator can lock the whole organisation out of role management.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/permissions` | `admin.roles.manage` | The catalogue, grouped by category. |
| GET | `/api/roles` | `admin.users.view` | Includes the granted permission keys per role. |
| POST | `/api/roles` | `admin.roles.manage` | Creates a non-system role. |
| PUT | `/api/roles/{id}` | `admin.roles.manage` | Renames; refused for system roles. |
| PUT | `/api/roles/{id}/permissions` | `admin.roles.manage` | Replaces the grant set. |
| DELETE | `/api/roles/{id}` | `admin.roles.manage` | Refused for system roles and for roles still held by a user. |

## Backend Tasks

### 1 — Permission catalogue query

**File:** `backend/src/CustomerSupport.Application/Security/Queries/GetPermissionsQuery.cs`

Return the `Permission` rows grouped by category, ordered by category then key, with the localized display name. Read from the database rather than `Permissions.All`, so the response includes the ids the grant editor needs.

### 2 — Role queries and commands

**File:** `backend/src/CustomerSupport.Application/Security/`

`GetRolesQuery` projects each role with its granted permission keys, plus a count of users holding it (needed to block deletion).

`UpdateRolePermissionsCommand` takes `RoleId` and `PermissionIds`. Compute the difference against the current grants and add or remove only what changed, so the audit trail shows the actual delta rather than a full replace.

Guard the lockout case in the handler:

```csharp
// Refuse a change that would strip role management from the caller's last role holding it.
if (removing.Contains(rolesManageId) && await WouldLoseRoleManagement(currentUser.UserId, roleId, ct))
{
    throw new ConflictException(
        "This change would remove your own ability to manage roles. Grant it to another role first.");
}
```

`DeleteRoleCommand` refuses when `IsSystem` is true or any user still holds the role.

### 3 — Registry drift test

**File:** `backend/tests/CustomerSupport.UnitTests/Security/PermissionRegistryTests.cs`

Parse `frontend/src/app/core/permissions.ts` for every quoted string matching `^[a-z]+(\.[a-z]+)+$` and assert the set equals `Permissions.All.Select(p => p.Key)`.

This is deliberately a test rather than code generation: it keeps both files readable and hand-editable, while making drift a build failure instead of a runtime 403 nobody can explain.

## Frontend Tasks

### 4 — Roles list

**File:** `frontend/src/app/features/admin/roles/role-list.page.ts`

Columns: name (localized), description, permission count, user count, system chip, actions. Actions: **Edit permissions**, **Rename** (hidden for system roles), **Delete** (hidden for system roles, disabled when the user count is above zero).

### 5 — Permission matrix editor

**File:** `frontend/src/app/features/admin/roles/role-permissions.page.ts`

Load the catalogue and the role's current grants. Render one collapsible panel per category with a checkbox per permission and a tri-state "select all in category" header checkbox.

Track the dirty set in a signal so the Save button is disabled until something changes, and show an inline notice:

> Permission changes take effect the next time the affected users refresh their session (within {{ accessTokenMinutes }} minutes) or sign in again.

On save, send the full `permissionIds` array. On a 409 (the self-lockout guard), surface the server message inline above the matrix rather than as a transient toast — it needs to be read.

### 6 — Routes and translations

**File:** `frontend/src/app/features/admin/roles/roles.routes.ts`, i18n files

Add `/admin/roles` behind `PERMISSIONS.administration.manageRoles`. Add an `admin.roles.*` namespace, and a `permissions.categories.*` map so category headings show as readable bilingual labels rather than the raw C# class names.

## Verification Steps

1. Open `/admin/roles` as a system administrator: all six seeded roles are listed with their permission and user counts.
2. Open the matrix for Agent: permissions are grouped by category and the currently granted ones are checked.
3. Grant `reports.tickets.view` to Agent and save. Confirm a `RolePermission` row was added and an audit entry records it.
4. Sign in as an agent, refresh the token, and confirm the Reports navigation entry now appears.
5. Revoke it again; the entry disappears after the next refresh, not before — this is the documented behaviour.
6. Try to rename or delete a system role: both are refused, and the UI does not offer the actions.
7. Try to remove `admin.roles.manage` from your own only role: the request is refused with a 409 explaining why.
8. Add a permission constant to `Permissions.cs` without adding it to `permissions.ts`: `dotnet test` fails on the drift test.
9. Restart the API after adding that constant: the new permission row is seeded automatically and appears in the catalogue.

## Done Criteria

- [ ] `/api/permissions` returns the catalogue grouped by category.
- [ ] Role CRUD works, with system roles protected from rename and deletion.
- [ ] `PUT /api/roles/{id}/permissions` applies the delta and audits each grant change.
- [ ] The self-lockout guard prevents an administrator removing their own role-management ability.
- [ ] The Angular matrix editor works, tracks dirty state, and explains the refresh delay.
- [ ] The registry drift test fails the build when the two permission lists disagree.
- [ ] Everything is translated in both languages.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
