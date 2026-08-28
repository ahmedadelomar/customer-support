# Story 03 — Audit log viewer, auth events and retention (Story: CS-1003-audit-logs)

## Prerequisites

- Story 01 must be complete.
- `AuditLogInterceptor` already writes entity mutations with redaction. **Do not rewrite it** — this story adds the read API, the viewer, the explicit authentication events and the retention job.

## Story Goal

An administrator can search the audit trail and read a clear before-and-after diff for any change.
Authentication events join entity mutations in the same trail, and a retention job keeps the table bounded.

The trail is append-only: no API exposes an update or delete, including to system administrators.

## Context — Read These Files First

1. `.squad/stories/security-administration/CS-1003-audit-logs/intake.md`.
2. [backend/src/CustomerSupport.Infrastructure/Persistence/Interceptors/AuditLogInterceptor.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Interceptors/AuditLogInterceptor.cs) — note `AuditedTypes` (the opt-in list) and `RedactedProperties`. Extend the lists here rather than logging everything.
3. [backend/src/CustomerSupport.Domain/Identity/AuditLog.cs](backend/src/CustomerSupport.Domain/Identity/AuditLog.cs) and its indexes in [OrganizationConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/OrganizationConfigurations.cs).
4. [backend/src/CustomerSupport.Api/Services/AuditContextAccessor.cs](backend/src/CustomerSupport.Api/Services/AuditContextAccessor.cs) — supplies IP, user agent and correlation id.
5. CS-1004 for `SystemSetting`, which holds the retention window.

## Product rules (from story)

- **Append-only.** No update or delete endpoint, ever. Retention deletes rows in a background job, not through the API.
- **Redaction is non-negotiable.** Password hashes, client secrets, encrypted credentials, webhook secrets and tokens never reach the trail.
- **Authentication events are audited explicitly:** `Login`, `LoginFailed`, `Logout`, `PermissionChanged`, `Export`.
- **A failed sign-in records the attempted username** but never the attempted password.
- **Retention** is driven by the `audit.retentionDays` setting (default 400 days). Zero means keep forever.
- **Reading the audit log is itself audited** as an `Export` action when results are exported, but not when merely browsed — otherwise browsing the log floods the log.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/audit-logs` | `admin.audit.view` | Paged. Filters: `from`, `to`, `userId`, `entityType`, `entityId`, `action`, `search`. |
| GET | `/api/audit-logs/{id}` | `admin.audit.view` | Full entry with parsed diff. |
| GET | `/api/audit-logs/export` | `admin.audit.view` + `reports.export` | CSV. Writes an `Export` audit entry. |
| GET | `/api/audit-logs/entity-types` | `admin.audit.view` | Distinct values, for the filter dropdown. |

## Backend Tasks

### 1 — Audit query slice

**File:** `backend/src/CustomerSupport.Application/Auditing/Queries/`

`GetAuditLogsQuery` follows the `GetCustomersQuery` shape. Default the date range to the last 7 days when none is supplied — an unbounded scan of an audit table is the classic way to take a database down.

Cap `PageSize` at 100 for this endpoint specifically, overriding the shared 200.

`GetAuditLogByIdQuery` deserialises `OldValues` and `NewValues` and returns a `FieldChange[]` of `{ field, oldValue, newValue }` so the client renders a diff without parsing JSON itself.

### 2 — Explicit authentication and export events

**File:** `backend/src/CustomerSupport.Application/Common/Interfaces/IAuditRecorder.cs` (new), used from the auth handlers

The interceptor only sees entity changes. Add:

```csharp
public interface IAuditRecorder
{
    Task RecordAsync(AuditAction action, string entityType, string? entityId = null,
                     object? metadata = null, string? userName = null, CancellationToken ct = default);
}
```

Call it from `LoginCommand` (both outcomes), `LogoutCommand`, `UpdateRolePermissionsCommand` (as `PermissionChanged`) and every export endpoint.

For `LoginFailed`, pass the attempted username through `userName` — `ICurrentUser.UserId` is null at that point, so the interceptor's usual source is unavailable.

### 3 — Retention job

**File:** `backend/src/CustomerSupport.Infrastructure/Jobs/AuditRetentionJob.cs`

A Quartz job running nightly. Read `audit.retentionDays`; if zero, exit. Otherwise delete in batches:

```csharp
const int batchSize = 5_000;
int deleted;
do
{
    deleted = await db.AuditLogs
        .Where(a => a.OccurredAt < cutoff)
        .Take(batchSize)
        .ExecuteDeleteAsync(ct);
} while (deleted == batchSize && !ct.IsCancellationRequested);
```

Batching matters: a single delete over months of rows takes a lock long enough to stall the API.

Register Quartz in `Infrastructure/DependencyInjection.cs` — the package is already referenced but not yet configured.

## Frontend Tasks

### 4 — Audit log viewer

**File:** `frontend/src/app/features/admin/audit/audit-log.page.ts`

A list page following the customers pattern. Columns: timestamp (datetime), actor, action (translated chip), entity type, entity id, IP address.

Filters: date range (defaulting to the last 7 days, matching the server), actor, entity type (from the dropdown endpoint), action, and free-text search.

Action chips: Create green, Update amber, Delete rose, Login and Logout slate, LoginFailed red, PermissionChanged violet, Export blue.

### 5 — Diff panel

**File:** `frontend/src/app/features/admin/audit/audit-detail.panel.ts`

A side panel opened from a row. Render a two-column table of changed fields only, with the old value struck through and the new value beside it. Show the trace id and correlation id in a monospace footer so an entry can be tied to application logs.

Values are untrusted data written by users — bind them as text, never as HTML.

### 6 — Route and translations

Add `/admin/audit` behind `PERMISSIONS.administration.viewAuditLogs`, an `admin.audit.*` namespace, and an `enums.auditAction.*` map (0–9) matching the C# `AuditAction` ordinals.

## Verification Steps

1. Edit a customer, then open `/admin/audit`: the update appears with the actor, entity and timestamp.
2. Open the entry: only the fields that actually changed are listed, with old and new values.
3. Change a user password and confirm no password hash appears anywhere in the entry.
4. Sign in successfully, then fail a sign-in: both appear, and the failed one records the attempted username and no password.
5. Change a role permission: a `PermissionChanged` entry is recorded.
6. Export the audit log to CSV: the file downloads and an `Export` entry is written.
7. Confirm no route exists to modify or delete an audit entry, including for a system administrator.
8. Set `audit.retentionDays` to 1, run the retention job manually, and confirm older rows are removed in batches while the API stays responsive.
9. Open the viewer with no date filter: the request defaults to the last 7 days rather than scanning the table.

## Done Criteria

- [ ] `/api/audit-logs` supports all documented filters, defaults to 7 days and caps page size at 100.
- [ ] Authentication, permission-change and export events are recorded explicitly through `IAuditRecorder`.
- [ ] Redaction is verified for every secret-bearing entity.
- [ ] The Angular viewer and diff panel work and are translated.
- [ ] The nightly retention job deletes in batches and honours `audit.retentionDays`.
- [ ] No API can modify or delete an audit entry.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
