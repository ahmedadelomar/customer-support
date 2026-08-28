# Story 01 — Users, roles and sign-in (Story: CS-1001-users-roles)

## Prerequisites

- The .NET SDK must be installed — it is not present on the development machine yet. See [backend/README.md](backend/README.md).
- No migration exists. This story creates `InitialCreate` covering the entire schema and adds the `TicketNumbers` and `CustomerCodes` sequences by hand in the generated `Up()`.
- Set `Jwt:Key` via user-secrets and `SEED_ADMIN_PASSWORD` in the environment before the first run, or the seeder will refuse to create the bootstrap administrator.

## Story Goal

An administrator can sign in, manage agent accounts, and assign roles. The API issues a JWT whose
claims carry the user's identity, branch scope and every granted permission, so authorisation needs no
database round trip per request. Deactivation replaces deletion, because tickets and audit rows reference
users permanently.

By the end of this story the app is genuinely usable: sign in at `/login`, land on the dashboard, and reach
Administration > Users.

## Context — Read These Files First

1. `.squad/stories/security-administration/CS-1001-users-roles/intake.md` — the acceptance criteria, especially lockout behaviour and the forced password change.
2. [backend/src/CustomerSupport.Infrastructure/Identity/ApplicationUser.cs](backend/src/CustomerSupport.Infrastructure/Identity/ApplicationUser.cs) — note `UserType`, `AccessibleBranchIds`, `AvailabilityStatus` and `MustChangePassword`; all four are already modelled.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs) — the six system roles and their permission categories are already seeded. Do not duplicate that logic.
4. [backend/src/CustomerSupport.Api/Services/CurrentUserService.cs](backend/src/CustomerSupport.Api/Services/CurrentUserService.cs) — the exact claim types the token must emit: `perm`, `branch`, `branches`, `dept`, `customer_id`.
5. [backend/src/CustomerSupport.Api/Program.cs](backend/src/CustomerSupport.Api/Program.cs) — JWT validation is already configured, including the query-string token path for the future hubs.
6. [frontend/src/app/core/auth/auth.models.ts](frontend/src/app/core/auth/auth.models.ts) — `AuthResult` is already typed on the client. **Implement the server to match this shape**, not the other way round.
7. [frontend/src/app/core/auth/auth.service.ts](frontend/src/app/core/auth/auth.service.ts) and [auth.guards.ts](frontend/src/app/core/auth/auth.guards.ts) — the client already calls `/api/auth/login` and `/api/auth/refresh`.
8. [frontend/src/app/features/auth/login.page.ts](frontend/src/app/features/auth/login.page.ts) — the sign-in screen already exists and needs no change.
9. [backend/src/CustomerSupport.Application/Common/Security/Permissions.cs](backend/src/CustomerSupport.Application/Common/Security/Permissions.cs) — the permission registry. Never invent a key; add it here first.
10. [backend/src/CustomerSupport.Application/Customers/Queries/GetCustomersQuery.cs](backend/src/CustomerSupport.Application/Customers/Queries/GetCustomersQuery.cs) — the canonical paged list handler: filter, count, sort against an allow-list, project, page.
11. [backend/src/CustomerSupport.Application/Customers/Commands/CreateCustomerCommand.cs](backend/src/CustomerSupport.Application/Customers/Commands/CreateCustomerCommand.cs) — the canonical command: record request, FluentValidation validator, handler, all in one file.
12. [backend/src/CustomerSupport.Api/Controllers/CustomersController.cs](backend/src/CustomerSupport.Api/Controllers/CustomersController.cs) — the controller shape to copy: bind, send, shape the response.

## Product rules (from story)

- **Never hard-delete a user.** Deactivate instead: tickets, audit rows and ticket events reference the user id forever.
- **A user must hold at least one role.** Reject creation and role removal that would leave a user with none.
- **Lockout:** 5 consecutive failures locks the account for 15 minutes. Both configured already in `AddIdentityCore`.
- **Sign-in responses must not distinguish** an unknown username from a wrong password from a locked account. One generic message, one status code.
- **Admin-created accounts** get `MustChangePassword = true`; every endpoint except change-password and sign-out returns 409 until it is cleared.
- **Refresh tokens are single-use and stored hashed.** Rotating one invalidates the old value, so a stolen token dies at the next legitimate refresh.
- **Access token lifetime** is `Jwt:AccessTokenMinutes` (default 60); refresh is `Jwt:RefreshTokenDays` (default 14).

## Data model

`ApplicationUser` and `ApplicationRole` already exist. Add one entity:

```csharp
// backend/src/CustomerSupport.Domain/Identity/RefreshToken.cs
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>SHA-256 of the issued token. The raw value is returned once and never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    /// <summary>Set when this token was rotated, pointing at its successor, so token-reuse can be detected.</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
```

Index: `(TokenHash) UNIQUE`, `(UserId, ExpiresAt)`. Register it on `IAppDbContext` and `AppDbContext`.

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/api/auth/login` | anonymous | Returns `AuthResult`. Rate-limited per IP. |
| POST | `/api/auth/refresh` | anonymous | Rotates the refresh token; revokes the old one. |
| POST | `/api/auth/logout` | authenticated | Revokes the presented refresh token. |
| POST | `/api/auth/change-password` | authenticated | Clears `MustChangePassword`. |
| GET | `/api/users` | `admin.users.view` | Paged; filters: search, roleId, departmentId, branchId, isActive. |
| GET | `/api/users/{id}` | `admin.users.view` | |
| POST | `/api/users` | `admin.users.manage` | Returns the new id. |
| PUT | `/api/users/{id}` | `admin.users.manage` | |
| POST | `/api/users/{id}/activate` | `admin.users.manage` | |
| POST | `/api/users/{id}/deactivate` | `admin.users.manage` | |
| PUT | `/api/users/{id}/roles` | `admin.roles.manage` | Replaces the role set. |
| GET | `/api/roles` | `admin.users.view` | For the role picker. |
| PUT | `/api/users/me/availability` | authenticated | Sets `AvailabilityStatus`. |

## Backend Tasks

### 1 — Create the initial migration

Run the `dotnet ef migrations add InitialCreate` command documented in [backend/README.md](backend/README.md), then edit the generated `Up()` to append the two sequences:

```csharp
migrationBuilder.Sql("CREATE SEQUENCE [dbo].[TicketNumbers] START WITH 1 INCREMENT BY 1;");
migrationBuilder.Sql("CREATE SEQUENCE [dbo].[CustomerCodes]  START WITH 1 INCREMENT BY 1;");
```

and the matching `DROP SEQUENCE` statements in `Down()`. `ReferenceNumberGenerator` reads these; without them, ticket and customer creation fails at runtime rather than at build time.

### 2 — Add the RefreshToken entity and token service

**File:** `backend/src/CustomerSupport.Domain/Identity/RefreshToken.cs`, `backend/src/CustomerSupport.Infrastructure/Services/TokenService.cs`

`ITokenService` in Application declares:

```csharp
Task<AuthResult> IssueAsync(ApplicationUser user, string? ip, CancellationToken ct);
Task<AuthResult> RefreshAsync(string refreshToken, string? ip, CancellationToken ct);
Task RevokeAsync(string refreshToken, CancellationToken ct);
```

`IssueAsync` builds the claims. Emit **one `perm` claim per granted permission**, resolved by joining the user's roles to `RolePermission` — `CurrentUserService` reads them exactly this way:

```csharp
claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
claims.Add(new Claim(ClaimTypes.Name, user.UserName!));
if (user.BranchId is { } branch) claims.Add(new Claim("branch", branch.ToString()));
if (!string.IsNullOrEmpty(user.AccessibleBranchIds)) claims.Add(new Claim("branches", user.AccessibleBranchIds));
if (user.DepartmentId is { } dept) claims.Add(new Claim("dept", dept.ToString()));
if (user.CustomerId is { } customerId) claims.Add(new Claim("customer_id", customerId.ToString()));
foreach (var key in permissionKeys) claims.Add(new Claim("perm", key));
```

`RefreshAsync` must **detect reuse**: if the presented token is already revoked, revoke the whole chain for that user and return 401. A replayed token means it leaked.

### 3 — Auth commands and controller

**File:** `backend/src/CustomerSupport.Application/Auth/`, `backend/src/CustomerSupport.Api/Controllers/AuthController.cs`

`LoginCommand`, `RefreshTokenCommand`, `LogoutCommand`, `ChangePasswordCommand`. These carry **no** `[RequirePermission]` — login is anonymous.

In the login handler use `SignInManager`-equivalent logic through `UserManager`: `CheckPasswordAsync`, then `AccessFailedAsync` on failure and `ResetAccessFailedCountAsync` on success. Return the same `UnauthorizedAccessException` for unknown user, wrong password and locked account, so the response cannot be used to enumerate accounts.

Decorate `AuthController` with `[AllowAnonymous]` on the login and refresh actions. Apply ASP.NET Core rate limiting to login: 10 attempts per IP per minute.

### 4 — User and role management slices

**File:** `backend/src/CustomerSupport.Application/Users/`

Mirror the Customers slice exactly. `GetUsersQuery` follows the `GetCustomersQuery` shape including the sortable-column allow-list. `CreateUserCommand` validates that `RoleIds` is non-empty and creates the user through `UserManager` so hashing and the password policy apply.

`DeactivateUserCommand` sets `IsActive = false` and revokes every refresh token for that user, otherwise a deactivated user keeps working until their access token expires.

`UpdateUserRolesCommand` replaces the role set in one transaction and rejects an empty result.

### 5 — Enforce MustChangePassword

**File:** `backend/src/CustomerSupport.Api/Infrastructure/MustChangePasswordMiddleware.cs`

Middleware placed after authentication: if the principal has the `must_change_password` claim and the request is not `/api/auth/change-password` or `/api/auth/logout`, short-circuit with 409 and a problem-details body carrying `"code": "must_change_password"`. Emit that claim from `IssueAsync` when the flag is set.

## Frontend Tasks

### 6 — Handle the forced password change

**File:** `frontend/src/app/features/auth/change-password.page.ts` (new), `frontend/src/app/core/http/error.interceptor.ts`

Add a `/change-password` route. In `errorInterceptor`, on a 409 whose body carries `code === 'must_change_password'`, navigate there instead of raising a toast.

The page posts to `/api/auth/change-password`, then re-issues the session and navigates to the dashboard.

### 7 — Users list and form

**File:** `frontend/src/app/features/admin/users/`

Clone the customers slice: `users.service.ts`, `user-list.page.ts`/`.html`, `user-form.page.ts`/`.html`, `users.routes.ts`.

Columns: display name (localized), username, email, roles (comma-joined), department, branch, availability, status chip, actions. Filters: search, role, department, active.

Row actions: **Edit** (`admin.users.manage`), **Deactivate** / **Activate** (`admin.users.manage`, whichever matches the current state), **Manage roles** (`admin.roles.manage`). There is deliberately **no delete action** — the rule is deactivate, not delete.

The form uses a multi-select for roles with a validator requiring at least one, matching the server.

### 8 — Register the admin area

**File:** [frontend/src/app/app.routes.ts](frontend/src/app/app.routes.ts)

Add an `/admin` branch alongside `/agent`, reusing `AgentShellPage` so administration lives in the same chrome:

```ts
{
  path: 'admin',
  canActivate: [authGuard],
  loadComponent: () => import('./layout/agent-shell/agent-shell.page').then((m) => m.AgentShellPage),
  children: [
    {
      path: 'users',
      canActivate: [permissionGuard],
      data: { titleKey: 'nav.users', permissions: [PERMISSIONS.administration.viewUsers] },
      loadChildren: () => import('./features/admin/users/users.routes').then((m) => m.USER_ROUTES),
    },
  ],
}
```

The sidebar entry already exists in `agent-shell.page.ts` and is already permission-gated; it will simply start resolving.

### 9 — Translations

**File:** [frontend/public/i18n/en.json](frontend/public/i18n/en.json), [frontend/public/i18n/ar.json](frontend/public/i18n/ar.json)

Add an `admin.users.*` namespace covering columns, fields, filters, actions, availability values and validation messages, plus `auth.changePassword.*`. Both files must gain the same keys — a key present in only one language renders as the raw key.

## Verification Steps

1. `dotnet build` in `backend/` completes with no warnings (the solution treats warnings as errors).
2. `dotnet ef database update` applies `InitialCreate`; confirm `TicketNumbers` and `CustomerCodes` exist with `SELECT NEXT VALUE FOR [dbo].[TicketNumbers]`.
3. Start the API with `SEED_ADMIN_PASSWORD` set. The log records that the bootstrap administrator was created.
4. Sign in as that administrator: the response carries an access token, a refresh token, and a `user.permissions` array holding every key.
5. Because `MustChangePassword` is set, any other API call returns 409 and the UI lands on `/change-password`. Change it, then confirm normal access resumes.
6. Decode the access token and confirm one `perm` claim per permission plus the `branch` claim.
7. Enter a wrong password 5 times: the 6th attempt is refused for 15 minutes, with the same message as a wrong password.
8. Create an agent with the Agent role. Sign in as them: Administration entries are absent from the sidebar, and navigating to `/admin/users` redirects to `/forbidden`.
9. Deactivate that agent while they are signed in; their next refresh fails and they are returned to `/login`.
10. Call `/api/auth/refresh` twice with the same refresh token: the second call returns 401 and every token for that user is revoked.
11. `npm run build` in `frontend/` completes; switch language and confirm the users screen renders in Arabic with RTL layout.

## Done Criteria

- [ ] `InitialCreate` migration is committed and includes both sequences.
- [ ] `RefreshToken` entity, `ITokenService` and `TokenService` exist, with single-use rotation and reuse detection.
- [ ] `/api/auth/login`, `/refresh`, `/logout` and `/change-password` behave as specified, and login is rate-limited.
- [ ] The access token carries `perm`, `branch`, `branches`, `dept` claims exactly as `CurrentUserService` reads them.
- [ ] User CRUD, activate/deactivate and role assignment are implemented behind their permissions.
- [ ] A user can never be left with zero roles, and no endpoint hard-deletes a user.
- [ ] `MustChangePassword` is enforced server-side and handled client-side.
- [ ] Angular admin users list and form work, are permission-gated, and are translated in both languages.
- [ ] Sign-in responses cannot be used to enumerate usernames.
- [ ] No TypeScript or C# build errors.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
