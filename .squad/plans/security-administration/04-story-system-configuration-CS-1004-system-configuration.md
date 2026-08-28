# Story 04 — System configuration settings (Story: CS-1004-system-configuration)

## Prerequisites

- Story 01 must be complete.
- `SystemSetting` already exists with a unique index on `(BranchId, Key)`.

## Story Goal

Operational behaviour becomes editable at runtime. Administrators change settings from a screen,
branches can override global values, secrets stay encrypted and write-only, and server code reads settings
through one typed, cached accessor.

## Context — Read These Files First

1. `.squad/stories/security-administration/CS-1004-system-configuration/intake.md`.
2. [backend/src/CustomerSupport.Domain/Identity/SystemSetting.cs](backend/src/CustomerSupport.Domain/Identity/SystemSetting.cs) — note `DataType`, `IsSecret` and `IsSystem`.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/OrganizationConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/OrganizationConfigurations.cs) — the `(BranchId, Key)` unique index.
4. CS-1204 (multi-branch) — the branch-override resolution rule defined here is reused there.

## Product rules (from story)

- **Resolution order:** the row for the caller's branch wins; otherwise the row with a null `BranchId`; otherwise the compiled-in default.
- **Secrets are write-only.** Reads return `"********"`. Submitting that exact placeholder is a no-op rather than an overwrite.
- **System settings can be edited but not deleted.**
- **A value that does not parse as its `DataType` is rejected** with a field-level error, before it is stored.
- **Every write is audited and invalidates the cache**, so no restart is needed.
- **A missing setting returns its documented default**, never null. Code should not have to null-check configuration.

## Data model

No new entity. Seed these system settings in `DbSeeder` (all global, `BranchId = null`):

| Key | Type | Default | Used by |
|---|---|---|---|
| `audit.retentionDays` | int | `400` | CS-1003 retention job |
| `tickets.numberPrefix` | string | `TCK` | `ReferenceNumberGenerator` |
| `tickets.autoCloseResolvedAfterDays` | int | `7` | CS-204 |
| `sla.warningThresholdPercent` | int | `80` | CS-501 |
| `attachments.maxBytes` | int | `26214400` | CS-104 |
| `attachments.allowedExtensions` | string | `pdf,png,jpg,jpeg,docx,xlsx,csv,txt` | CS-104 |
| `portal.enabled` | bool | `true` | CS-801 |
| `ai.enabled` | bool | `false` | Section 7 |
| `csat.enabled` | bool | `true` | CS-904 |
| `csat.surveyExpiryDays` | int | `14` | CS-805 |

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/settings` | `admin.settings.manage` | Grouped by category; secrets masked. |
| GET | `/api/settings/{key}` | `admin.settings.manage` | Resolved value plus whether it is inherited. |
| PUT | `/api/settings/{key}` | `admin.settings.manage` | Body: `{ value, branchId? }`. |
| DELETE | `/api/settings/{key}/override` | `admin.settings.manage` | Removes a branch override, restoring inheritance. |
| GET | `/api/settings/public` | anonymous | The small allow-listed subset the portal needs before sign-in. |

## Backend Tasks

### 1 — Typed settings accessor

**File:** `backend/src/CustomerSupport.Application/Common/Interfaces/ISettingsProvider.cs`, implemented in Infrastructure

```csharp
public interface ISettingsProvider
{
    Task<T> GetAsync<T>(string key, T defaultValue, Guid? branchId = null, CancellationToken ct = default);
    Task SetAsync(string key, string? value, Guid? branchId, CancellationToken ct = default);
    void Invalidate(string key);
}
```

Back it with `IMemoryCache`, keyed `settings:{branchId ?? "global"}:{key}`, 10-minute sliding expiry, invalidated on write. Resolve in the documented order: branch row, then global row, then `defaultValue`.

Decrypt `IsSecret` values through `IDataProtectionProvider` on read; encrypt on write.

Register a single `SettingKeys` static class holding every key and its default, so the seeded table and the code defaults cannot disagree.

### 2 — Settings queries and commands

**File:** `backend/src/CustomerSupport.Application/Settings/`

`GetSettingsQuery` returns settings grouped by category, each with the resolved value, the source (`Branch`, `Global` or `Default`), the data type, and the secret and system flags. **Mask secrets in the projection**, not in the controller, so no future caller can bypass it.

`UpdateSettingCommand` validates the value against `DataType` before writing:

```csharp
var parsed = setting.DataType switch
{
    "int" => int.TryParse(request.Value, out _),
    "bool" => bool.TryParse(request.Value, out _),
    "json" => IsValidJson(request.Value),
    _ => true,
};
if (!parsed) throw new ValidationException(new Dictionary<string, string[]>
{
    ["value"] = [$"Value must be a valid {setting.DataType}."],
});
```

If `IsSecret` and the submitted value equals the mask, return without writing. Otherwise encrypt, save, and call `Invalidate`.

### 3 — Seed the system settings

**File:** [backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs)

Add `SeedSystemSettingsAsync` following the existing idempotent pattern: insert only the keys from `SettingKeys` that have no global row yet, so an administrator's edits are never overwritten on restart. Call it from `SeedAsync` after `SeedPermissionsAsync`.

## Frontend Tasks

### 4 — Settings screen

**File:** `frontend/src/app/features/admin/settings/settings.page.ts`

One collapsible section per category. Render the editor from `dataType`: text input for string, number input for int, toggle for bool, a monospace textarea with JSON validation for json, and a password input for secret.

For each setting show an inheritance badge: **Global**, **Branch override**, or **Default**. When a branch override exists, offer **Remove override**, which calls the delete endpoint and reverts to the inherited value.

Save per section rather than per field, so a related group of changes lands together, and disable Save until the section is dirty.

### 5 — Route and translations

Add `/admin/settings` behind `PERMISSIONS.administration.manageSettings` — the sidebar entry already exists. Add an `admin.settings.*` namespace, plus a bilingual label and help text per seeded key so the screen is self-explanatory rather than a raw key/value grid.

## Verification Steps

1. Open `/admin/settings`: every seeded setting appears under its category with the right editor for its type.
2. Change `attachments.maxBytes` and save; the audit trail records it.
3. Confirm the new value takes effect on the next request with no restart.
4. Enter `abc` in an int setting: the request is rejected with a field-level error and nothing is stored.
5. Create a branch override for `sla.warningThresholdPercent`; users in that branch resolve the override while others still resolve the global value.
6. Remove the override and confirm the value reverts to inherited.
7. Set a secret setting, reload, and confirm it renders as `********`. Save the form unchanged and confirm the stored value is unchanged.
8. Attempt to delete a system setting: the action is not offered, and calling the endpoint directly is refused.
9. Delete a setting row directly in the database and confirm the code default is returned rather than a null reference.

## Done Criteria

- [ ] `ISettingsProvider` resolves branch, then global, then default, and caches with invalidation on write.
- [ ] Secrets are encrypted at rest, masked on read, and unchanged when the mask is resubmitted.
- [ ] Values are validated against their declared data type before storage.
- [ ] System settings can be edited but not deleted.
- [ ] `SettingKeys` is the single source for keys and defaults, and the seeder is idempotent.
- [ ] The Angular settings screen renders the correct editor per type and shows inheritance clearly.
- [ ] All writes are audited, and both languages are covered.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
