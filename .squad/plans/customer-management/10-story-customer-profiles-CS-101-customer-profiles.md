# Story 10 — Customer profiles (reference vertical slice) (Story: CS-101-customer-profiles)

## Prerequisites

- **Most of this story already exists.** Read the files listed below before writing anything — the task is to finish and verify the slice, not to rebuild it.
- CS-1001 must be complete for authentication; without a token the endpoints return 401.
- CS-1204 should be complete, or branch scoping must be added as part of this story.

## Story Goal

A complete, working customer-profile feature — and the canonical example every other feature in this
product copies. When another story says "follow the customers slice", it means these files.

Remaining work: apply branch scoping to the queries, add CSV export, wire the profile header counts to real
data, and add the missing tests.

## Context — Read These Files First

1. `.squad/stories/customer-management/CS-101-customer-profiles/intake.md`.
2. **[backend/src/CustomerSupport.Application/Customers/Queries/GetCustomersQuery.cs](backend/src/CustomerSupport.Application/Customers/Queries/GetCustomersQuery.cs)** — the canonical list handler. Note the sortable-column allow-list, the open-ticket-count join computed once and used for both filtering and display, and the count taken before paging.
3. **[backend/src/CustomerSupport.Application/Customers/Commands/CreateCustomerCommand.cs](backend/src/CustomerSupport.Application/Customers/Commands/CreateCustomerCommand.cs)** — request record, validator and handler in one file; duplicate detection before insert; contacts created alongside the profile.
4. [backend/src/CustomerSupport.Application/Customers/Commands/DeleteCustomerCommand.cs](backend/src/CustomerSupport.Application/Customers/Commands/DeleteCustomerCommand.cs) — the open-ticket guard, and why `Remove()` becomes a soft delete.
5. [backend/src/CustomerSupport.Api/Controllers/CustomersController.cs](backend/src/CustomerSupport.Api/Controllers/CustomersController.cs) — thin controller; no business logic, no authorisation attributes (the pipeline handles both).
6. **[frontend/src/app/features/agent/customers/customer-list.page.ts](frontend/src/app/features/agent/customers/customer-list.page.ts)** — URL-as-state, language-reactive `columns`, permission-aware row actions, signal-based loading.
7. [frontend/src/app/features/agent/customers/customer-form.page.ts](frontend/src/app/features/agent/customers/customer-form.page.ts) — cross-field validators mirroring the server, and `#applyServerErrors` mapping a 400 problem-details `errors` map onto controls.
8. [frontend/src/app/features/agent/customers/customers.service.ts](frontend/src/app/features/agent/customers/customers.service.ts) — plain `HttpClient`, no state.

## Product rules (from story)

- **Code format `CUS-000123`**, allocated from the `CustomerCodes` sequence. Immutable once assigned.
- **At least one of email or phone is required** — an unreachable customer cannot be supported.
- **Company and government customers require a company name.**
- **Duplicate email or phone is refused with 409**, not silently merged. Split profiles split the interaction history, which is worse than a rejected create.
- **Blocking requires a reason** and stops new inbound tickets without touching history.
- **Delete is refused while open tickets exist**, and is always a soft delete.
- **All list state lives in the URL** so a filtered view is shareable and survives a refresh.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Apply branch scoping

**File:** `GetCustomersQuery.cs`, `GetCustomerByIdQuery.cs`, `DeleteCustomerCommand.cs`

Insert `.WhereBranchAccessible(currentUser)` into each query. In `GetCustomerByIdQuery` it must come **before** the id match so an out-of-scope customer returns 404 rather than 403:

```csharp
var customer = await db.Customers
    .AsNoTracking()
    .WhereBranchAccessible(currentUser)
    .Include(c => c.Contacts.Where(x => !x.IsDeleted))
    .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
    ?? throw new NotFoundException(nameof(Customer), request.Id);
```

Inject `ICurrentUser` into the query handlers, which currently take only `IAppDbContext`.

### 2 — Block-aware ticket creation and a CSV export

Add `BlockCustomerCommand` and `UnblockCustomerCommand` (permission `customers.update`), both requiring a reason on block. When CS-201 lands, its create-ticket handler must reject a blocked customer with 409.

Add `GET /api/customers/export` behind `customers.export`, streaming CSV with the same filters as the list query. Write an `Export` audit entry (CS-1003) and cap the export at 50,000 rows, returning a clear error above that rather than timing out.

### 3 — Tests for the reference slice

**File:** `backend/tests/CustomerSupport.UnitTests/Customers/`

Because this slice is the template, its tests are the template too:

- `CreateCustomerCommandValidator`: rejects no-contact, rejects company type without a company name, rejects a malformed phone.
- `CreateCustomerCommandHandler`: throws `ConflictException` on a duplicate email; normalises email to lower case and strips phone separators.
- `DeleteCustomerCommandHandler`: throws `ConflictException` when open tickets exist; soft-deletes otherwise.
- `GetCustomersQueryHandler`: an unknown `SortBy` falls back to `CreatedAt` descending rather than throwing.

## Frontend Tasks

### 4 — Wire the real header counts

**File:** [frontend/src/app/features/agent/customers/customer-list.page.ts](frontend/src/app/features/agent/customers/customer-list.page.ts)

`activeCount` and `withOpenTicketsCount` currently count **the current page only**, which is misleading beside a total that spans every page.

Either add a `/api/customers/statistics` endpoint returning the true totals, or relabel the tiles to say "on this page". Prefer the endpoint — the same pattern is needed by every list screen with KPI tiles.

### 5 — Block/unblock action and export button

Add **Block** and **Unblock** to the row action menu and the detail page, gated on `customers.update`, with a dialog capturing the required reason.

Add an **Export** button to the page header behind `customers.export`, passing the current filters. Trigger the download through an authenticated `HttpClient` blob request rather than a plain link, because a plain link cannot carry the bearer token.

### 6 — Spec for the list page

**File:** `frontend/src/app/features/agent/customers/customer-list.page.spec.ts`

Cover: the component creates; `#derivedStatus` returns blocked before inactive; `#statusToQuery` expands each filter value correctly; and the delete action is disabled when `openTicketCount > 0`.

## Verification Steps

1. `dotnet test` passes, including the four new handler tests.
2. Create a customer with an email only: succeeds. With neither email nor phone: rejected with a field-level error.
3. Create a second customer with the same email: 409, and the message names the conflict.
4. Create a company customer without a company name: rejected.
5. Filter, sort and page the list, then copy the URL into a new tab: the same view is restored.
6. Switch language: headers, action labels and status chips re-translate without a refetch.
7. As a user in branch A, request a branch B customer by id: 404.
8. Block a customer with a reason; the reason shows on the profile.
9. Delete a customer with an open ticket: refused, with the count in the message. Close it and retry: succeeds and the row disappears.
10. Export with filters applied: the CSV matches the filtered list and an `Export` audit entry is written.

## Done Criteria

- [ ] Branch scoping is applied to all three customer queries, with out-of-scope reads returning 404.
- [ ] Block and unblock work and require a reason.
- [ ] CSV export honours the current filters, is capped, and is audited.
- [ ] Header KPI tiles show true totals or are honestly labelled.
- [ ] Handler and validator tests pass and serve as the template for other features.
- [ ] The slice is confirmed as the reference: no other feature needs to invent its own list or form pattern.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
