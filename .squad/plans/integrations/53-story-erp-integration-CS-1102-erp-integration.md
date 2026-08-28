# Story 53 — ERP synchronisation framework (Story: CS-1102-erp-integration)

## Prerequisites

- CS-101 and CS-1004 must be complete.
- `IntegrationConnection` and `IntegrationSyncLog` already exist, including the watermark and failure counter.
- **The CRM must never depend on the ERP being reachable.** Every call in this story is enrichment.

## Story Goal

A configurable, resilient synchronisation framework with one reference adapter: scheduled customer
import with configurable field mapping, on-demand context lookup on the ticket screen, and a circuit breaker
so an ERP outage is invisible to agents beyond a clear message.

## Context — Read These Files First

1. `.squad/stories/integrations/CS-1102-erp-integration/intake.md`.
2. [backend/src/CustomerSupport.Domain/Integrations/IntegrationConnection.cs](backend/src/CustomerSupport.Domain/Integrations/IntegrationConnection.cs) — `CredentialsEncrypted` is write-only; `ConsecutiveFailureCount` drives the breaker.
3. [backend/src/CustomerSupport.Domain/Integrations/IntegrationSyncLog.cs](backend/src/CustomerSupport.Domain/Integrations/IntegrationSyncLog.cs) — `Watermark` enables incremental resume.
4. [backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs](backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs) (CS-102) — imported contacts must normalise identically or matching fails.

## Product rules (from story)

- **Enrichment, never a dependency.** A failed or slow ERP call leaves every screen fully usable.
- **A failed record does not fail the run.** Record it and continue.
- **Incremental sync resumes from the last successful watermark.**
- **Field mapping is configuration**, so a new ERP field needs no deployment.
- **Credentials are encrypted and never returned** by any endpoint.
- **The circuit breaker opens after the configured consecutive failures**, marks the connection degraded, alerts administrators, and stops calling until a probe succeeds.
- **Imported contacts use the shared normaliser**, or inbound message matching will silently fail for imported customers.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Adapter abstraction and reference implementation

**File:** `backend/src/CustomerSupport.Application/Integrations/Erp/IErpAdapter.cs`

```csharp
public interface IErpAdapter
{
    Task<bool> TestConnectionAsync(IntegrationConnection connection, CancellationToken ct);

    /// <summary>Streams changed records since the watermark, so a large sync does not load into memory.</summary>
    IAsyncEnumerable<ErpCustomerRecord> GetCustomersAsync(
        IntegrationConnection connection, string? sinceWatermark, CancellationToken ct);

    Task<IReadOnlyList<ErpOrderSummary>> GetOrdersAsync(
        IntegrationConnection connection, string externalCustomerId, int take, CancellationToken ct);
}
```

Resolve the adapter from `IntegrationConnection.Provider` through a keyed registration, so adding an ERP is a new class plus a registration line.

Ship one reference adapter — a generic REST adapter reading its endpoint paths and auth style from `SettingsJson` — which covers many ERPs without a bespoke class.

### 2 — Configurable field mapping and sync

Store mapping in `SettingsJson`:

```json
{ "customerMapping": {
    "code": "CustomerNumber",
    "displayNameEn": "NameEnglish",
    "displayNameAr": "NameArabic",
    "primaryEmail": "Email",
    "primaryPhone": "Mobile",
    "tier": "CustomerClass"
} }
```

The sync loop records per-record failures without aborting:

```csharp
await foreach (var record in adapter.GetCustomersAsync(connection, log.Watermark, ct))
{
    try
    {
        await UpsertCustomerAsync(record, mapping, ct);
        log.RecordsWritten++;
    }
    catch (Exception ex)
    {
        // One bad record must not lose the rest of the run.
        log.RecordsFailed++;
        failures.Add(new { record.ExternalId, Error = ex.Message });
        logger.LogWarning(ex, "ERP record {ExternalId} failed to sync", record.ExternalId);
    }

    log.RecordsRead++;
    if (log.RecordsRead % 100 == 0) await db.SaveChangesAsync(ct);   // checkpoint progress
}

// Only advance the watermark when the run was clean enough to trust.
if (log.RecordsFailed == 0) log.Watermark = newWatermark;
```

Match customers on the ERP external id first, then on normalised email — using `ContactNormalizer`, never a second normalisation.

Conflict rule from configuration: `erp-wins`, `crm-wins`, or `newest-wins`. Log every conflict regardless of the rule chosen.

### 3 — Circuit breaker and on-demand lookup

```csharp
public async Task<T?> ExecuteAsync<T>(IntegrationConnection connection, Func<Task<T>> call, CancellationToken ct)
{
    if (connection.Status == IntegrationStatus.Degraded &&
        connection.LastHealthCheckAt > clock.UtcNow.AddMinutes(-5))
    {
        return default;   // breaker open — fail fast rather than making the agent wait
    }

    try
    {
        var result = await call().WaitAsync(TimeSpan.FromSeconds(5), ct);
        if (connection.ConsecutiveFailureCount > 0) await ResetAsync(connection, ct);
        return result;
    }
    catch (Exception ex)
    {
        await RecordFailureAsync(connection, ex, ct);   // opens the breaker at the threshold
        return default;
    }
}
```

The five-second timeout is deliberate: the ticket screen must render regardless, so a slow ERP is treated the same as a failed one.

The order-lookup endpoint returns an empty result with a `degraded: true` flag when the breaker is open, so the UI can say "ERP context unavailable" rather than showing an empty list that looks like "no orders".

## Frontend Tasks

### 4 — Connection admin and ERP panel

The connection form covers provider, base URL, credentials (write-only, masked on read), sync schedule, conflict rule and the field-mapping editor — a two-column mapper listing CRM fields against ERP field names.

**Test connection** with an inline result, and a **Sync now** action with live progress.

The sync log lists runs with status, counts and duration, expandable to per-record failures.

On the ticket screen, an ERP context panel loading independently of the ticket: skeleton while loading, orders when available, and an explicit "ERP context unavailable" message when `degraded` is set — never an empty list, which would read as "this customer has no orders".

## Verification Steps

1. Configure a connection against a stub ERP: test connection reports success, and the credentials are never returned by the API.
2. Run a sync of 100 customers: all import according to the mapping.
3. Make 5 records invalid: the run completes, 95 succeed, 5 are logged with reasons, and the watermark does not advance.
4. Fix them, re-run: only changed records are read.
5. Change a mapped field name in configuration: the next sync uses it with no deployment.
6. Confirm an imported email is normalised identically to one entered by hand — verify inbound email matching works for an imported customer.
7. Change a record on both sides and sync: the configured conflict rule applies and the conflict is logged.
8. Open a ticket for a synced customer: ERP orders appear in the panel.
9. Make the ERP hang: the ticket screen renders in normal time and the panel shows "unavailable".
10. Fail the ERP repeatedly: the breaker opens, the connection is marked degraded, and administrators are alerted.
11. Restore the ERP: the breaker closes on the next probe.

## Done Criteria

- [ ] Adapter abstraction with a generic REST reference implementation, resolved by provider key.
- [ ] Field mapping is configuration, applied without deployment.
- [ ] Per-record failures are logged without aborting the run, and the watermark advances only on clean runs.
- [ ] Imported contacts use the shared normaliser.
- [ ] Conflicts follow the configured rule and are always logged.
- [ ] The circuit breaker fails fast, alerts, and recovers.
- [ ] The ticket screen is unaffected by ERP slowness, and degraded state is shown explicitly.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
