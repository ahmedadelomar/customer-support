# Story 08 — Branch scoping and the branch switcher (Story: CS-1204-multi-branch)

## Prerequisites

- CS-1001 must be complete: the `branch` and `branches` claims come from its token service.
- `WhereBranchAccessible` already exists in `QueryableExtensions`. This story applies it everywhere and adds the guard against handlers that forget.

## Story Goal

Data is scoped by branch. A single-branch user sees their own branch plus global records; a
multi-branch user gets a switcher; a head-office user sees everything. Out-of-scope records return 404, not
403, so their existence is never disclosed.

## Context — Read These Files First

1. `.squad/stories/platform/CS-1204-multi-branch/intake.md`.
2. [backend/src/CustomerSupport.Application/Common/Interfaces/QueryableExtensions.cs](backend/src/CustomerSupport.Application/Common/Interfaces/QueryableExtensions.cs) — the existing `WhereBranchAccessible`, including the "empty means unrestricted" rule.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs](backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs) — the class remark explaining **why** branch scoping is not a global query filter. Read it before proposing to make it one.
4. [backend/src/CustomerSupport.Domain/Common/ITenantScoped.cs](backend/src/CustomerSupport.Domain/Common/ITenantScoped.cs) — `null` means global to all branches.
5. [backend/src/CustomerSupport.Api/Services/CurrentUserService.cs](backend/src/CustomerSupport.Api/Services/CurrentUserService.cs) — `BranchId` and `AccessibleBranchIds`.

## Product rules (from story)

- **A null `BranchId` means global** and is visible from every branch. Lookups (priorities, statuses, channels) are global; customers and tickets are branch-scoped.
- **An empty `AccessibleBranchIds` means unrestricted** — that is the head-office case, and it is what `WhereBranchAccessible` already implements.
- **New records take the caller's currently active branch.**
- **Out-of-scope reads return 404, never 403.** A 403 confirms the record exists.
- **Scoping is opt-in per query**, deliberately. Background jobs and cross-branch reports need the whole table.
- **Switching branch re-issues the token** with the new active branch, so server-side scoping follows the UI without a second source of truth.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Branch CRUD and the switch endpoint

**File:** `backend/src/CustomerSupport.Application/Organization/Branches/`

Standard CRUD behind `admin.branches.manage`. Deactivation is refused while active users or open tickets remain.

Add `POST /api/auth/switch-branch` taking `{ branchId }`. Verify the branch is in the caller's accessible set (or that they are unrestricted), then re-issue the token with the new `branch` claim. Rejecting an inaccessible branch here is essential — otherwise the switcher becomes a privilege-escalation vector.

### 2 — Apply scoping to every tenant-aware query

Every handler reading an `ITenantScoped` entity must call `.WhereBranchAccessible(currentUser)`. Work through the query slices systematically: customers, tickets, KB articles, quick replies, channel accounts, SLA policies, assignment and escalation rules, dashboards, reports, integrations.

For single-record reads, scoping must produce a 404:

```csharp
var customer = await db.Customers
    .WhereBranchAccessible(currentUser)
    .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
    ?? throw new NotFoundException(nameof(Customer), request.Id);
```

Because the filter is applied before the id match, an out-of-scope record is indistinguishable from a missing one.

### 3 — Guard against handlers that forget scoping

**File:** `backend/tests/CustomerSupport.UnitTests/Security/BranchScopingTests.cs`

The cost of opt-in scoping is that a handler can silently omit it. Make that a test failure:

Reflect over every `IRequestHandler` in the Application assembly whose request type is a query, and assert that any handler touching a `DbSet<T>` where `T : ITenantScoped` also references `WhereBranchAccessible`. A source-level check on the compiled method body is brittle; the pragmatic version is an integration test that seeds two branches and asserts each list endpoint returns only in-scope rows.

Prefer the integration test: it tests the behaviour rather than the implementation, and it catches the case where scoping is applied to the wrong query.

## Frontend Tasks

### 4 — Branch switcher

**File:** [frontend/src/app/layout/agent-shell/agent-shell.page.ts](frontend/src/app/layout/agent-shell/agent-shell.page.ts)

Show a branch dropdown in the top bar only when the user has more than one accessible branch, or is unrestricted. Selecting one calls `/api/auth/switch-branch`, applies the new token, and reloads the current route so lists refetch under the new scope.

Store the active branch in `AuthService` alongside the user so it survives a refresh.

### 5 — Branches admin screen

**File:** `frontend/src/app/features/admin/organization/branch-list.page.ts`

List and form behind `PERMISSIONS.administration.manageBranches`. Fields: code, bilingual name, time zone (from a curated IANA list, defaulting to `Asia/Riyadh`), address, phone, active. Deactivation is offered but refused server-side when the branch is in use — surface that message inline.

## Verification Steps

1. Create a second branch and a user assigned to it. Sign in as them: they see only their own branch's customers and tickets, plus global lookups.
2. Attempt to open a customer from the other branch by id: 404, not 403.
3. Give the user access to both branches: the switcher appears and switching changes the visible data.
4. Create a record while switched to branch B: it is stored with branch B.
5. Attempt to switch to a branch not in the accessible set by calling the endpoint directly: refused.
6. Sign in as a head-office user with no restrictions: all branches are visible and reports can group by branch.
7. Run the branch-scoping integration test with two seeded branches: every list endpoint returns only in-scope rows.
8. Confirm a background job (for example the SLA breach sweep) still sees tickets across all branches.

## Done Criteria

- [ ] Branch CRUD works and deactivation is blocked while in use.
- [ ] `switch-branch` re-issues the token and rejects inaccessible branches.
- [ ] Every tenant-aware query applies `WhereBranchAccessible`.
- [ ] Out-of-scope single-record reads return 404.
- [ ] The branch switcher appears only for multi-branch users.
- [ ] An integration test proves scoping across two branches for every list endpoint.
- [ ] Background jobs remain unscoped by design.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
