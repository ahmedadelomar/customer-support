# Story 52 — Public API with client credentials and idempotency (Story: CS-1101-public-apis)

## Prerequisites

- CS-1001, CS-101 and CS-201 must be complete.
- `ApiClient` already exists with scopes, IP ranges, rate limit and secret hash.
- **Design the public DTOs as a separate, versioned contract.** Reusing internal DTOs is the decision that makes every later refactor a breaking change.

## Story Goal

External systems can integrate: authenticate with client credentials, work within scopes, retry safely
with idempotency keys, and read documentation generated from the code.

## Context — Read These Files First

1. `.squad/stories/integrations/CS-1101-public-apis/intake.md`.
2. [backend/src/CustomerSupport.Domain/Integrations/ApiClient.cs](backend/src/CustomerSupport.Domain/Integrations/ApiClient.cs) — read the class remark: only the secret hash is stored, so a lost secret is rotated, not recovered.
3. [backend/src/CustomerSupport.Application/Common/Security/Permissions.cs](backend/src/CustomerSupport.Application/Common/Security/Permissions.cs) — scopes are a subset of these keys.
4. [backend/src/CustomerSupport.Api/Infrastructure/GlobalExceptionHandler.cs](backend/src/CustomerSupport.Api/Infrastructure/GlobalExceptionHandler.cs) — the problem-details shape the public API extends with a stable `code`.
5. [backend/src/CustomerSupport.Api/Program.cs](backend/src/CustomerSupport.Api/Program.cs) — OpenAPI and Scalar are already registered.

## Product rules (from story)

- **The secret is shown once.** Only its hash and a four-character hint are stored.
- **Scopes are a subset of the permission keys**, so there is one authorisation vocabulary rather than two.
- **Idempotency keys are honoured for 24 hours** on every write. An external caller that times out must be able to retry safely.
- **Errors carry a stable machine-readable `code`**, not only a human message — a partner cannot branch on prose.
- **API clients are branch-scoped** exactly as users are.
- **Rate limits are per client**, with `Retry-After` on 429.
- **The public contract is versioned under `/api/v1/`** and changes additively.

## Data model

`ApiClient` already exists. Add one entity for idempotency:

```csharp
// backend/src/CustomerSupport.Domain/Integrations/IdempotencyRecord.cs
public class IdempotencyRecord : BaseEntity
{
    public Guid ApiClientId { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>Hash of method + path + body, so the same key with a different payload is rejected.</summary>
    public string RequestHash { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
```

Unique index on `(ApiClientId, Key)`; an index on `ExpiresAt` for cleanup.

## API contract

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/v1/oauth/token` | client credentials | Returns a scoped access token. |
| GET/POST | `/api/v1/tickets` | scope `tickets.view` / `tickets.create` | Public DTOs. |
| GET/PATCH | `/api/v1/tickets/{id}` | scope | |
| POST | `/api/v1/tickets/{id}/messages` | scope `tickets.reply` | |
| GET/POST | `/api/v1/customers` | scope `customers.*` | |
| GET | `/api/v1/openapi.json` | anonymous | Generated from code. |
| GET/POST/DELETE | `/api/api-clients` | `integrations.apiclients.manage` | Internal admin. |
| POST | `/api/api-clients/{id}/rotate-secret` | `integrations.apiclients.manage` | |

## Backend Tasks

### 1 — Client credentials and scoped tokens

`POST /api/v1/oauth/token` accepts `client_id` and `client_secret`, verifies against the stored hash with a fixed-time comparison, checks active state, expiry and the IP allow-list, then issues a JWT carrying `client_id`, the scopes as `perm` claims and the branch.

Because scopes become `perm` claims, the existing `AuthorizationBehaviour` enforces them with no second mechanism.

Generate secrets as 32 bytes of cryptographic randomness, base64url-encoded, returned once. Store the hash and the last four characters as `SecretHint`.

Rotation issues a new secret and invalidates the old immediately — no grace period unless the user explicitly asks for one, since a grace period is exactly how a leaked secret stays usable.

### 2 — Idempotency middleware

**File:** `backend/src/CustomerSupport.Api/Infrastructure/IdempotencyMiddleware.cs`

On any `/api/v1/*` write carrying `Idempotency-Key`:

```csharp
var hash = Hash(request.Method, request.Path, bodyBytes);
var existing = await db.IdempotencyRecords
    .FirstOrDefaultAsync(r => r.ApiClientId == clientId && r.Key == key, ct);

if (existing is not null)
{
    // Same key, different payload is a caller bug — surface it rather than replaying the wrong result.
    if (existing.RequestHash != hash)
    {
        return Problem(422, "idempotency_key_reuse",
            "This Idempotency-Key was already used with a different request body.");
    }

    context.Response.StatusCode = existing.ResponseStatusCode;
    await context.Response.WriteAsync(existing.ResponseBody ?? string.Empty, ct);
    return;   // replay, do not re-execute
}
```

Store the response after a successful execution with a 24-hour expiry. Insert a placeholder row **before** executing so two concurrent retries cannot both run — the unique index makes the second wait or fail rather than duplicating the write.

A nightly job deletes expired records.

### 3 — Public DTOs, errors and documentation

Define public DTOs under `Api/V1/Models/` with no reference to internal DTOs or domain entities. Expose stable string enums (`"open"`, `"resolved"`) rather than the internal numeric ordinals — a partner should not break because an enum gained a member.

Extend problem-details with a stable `code` (`ticket_not_found`, `customer_blocked`, `validation_failed`, `rate_limit_exceeded`, `idempotency_key_reuse`), documented as a table in the API docs.

Rate limit per client with ASP.NET Core rate limiting partitioned by client id, emitting `Retry-After`, `X-RateLimit-Limit` and `X-RateLimit-Remaining`.

Configure OpenAPI to emit a separate `/api/v1/openapi.json` document covering only the public surface, with examples on every operation, served through the already-registered Scalar UI.

## Frontend Tasks

### 4 — API client administration

**File:** `frontend/src/app/features/admin/integrations/api-clients/`

A list showing name, client id, scopes count, rate limit, last used and active state.

The form has a scope picker grouped by category, mirroring the role editor so the vocabulary is familiar, plus an optional IP allow-list and rate limit.

On create or rotate, show the secret **once** in a modal with a copy button and an explicit warning that it cannot be retrieved again. Require the administrator to tick "I have stored this secret" before closing — a secret lost at this moment costs a rotation and a partner outage.

Link to the API documentation from the list.

## Verification Steps

1. Create an API client and obtain a token with its credentials.
2. Call an in-scope endpoint: succeeds. Call an out-of-scope endpoint: 403 naming the missing scope.
3. Call from an IP outside the allow-list: rejected.
4. Exceed the rate limit: 429 with `Retry-After` and the rate-limit headers.
5. POST a ticket with an idempotency key, then repeat the identical request: one ticket, and the original response replayed.
6. Repeat the key with a different body: 422 with `idempotency_key_reuse`.
7. Fire two identical requests concurrently with the same key: exactly one ticket is created.
8. Rotate the secret: the old one stops working immediately and the rotation is audited.
9. Fetch the OpenAPI document: it covers only the public surface, with examples.
10. Confirm public responses expose string statuses rather than numeric ordinals.
11. Confirm an API client sees only its own branch's data.
12. Confirm no public DTO is a reused internal DTO.

## Done Criteria

- [ ] Client credentials issue scoped tokens enforced by the existing authorisation pipeline.
- [ ] Secrets are hashed, shown once, and rotatable with immediate invalidation.
- [ ] Idempotency works, including under concurrent retries, and rejects key reuse with a different body.
- [ ] Public DTOs are separate, versioned and use stable string enums.
- [ ] Errors carry documented stable codes.
- [ ] Rate limiting is per client with the standard headers.
- [ ] OpenAPI documentation is generated and covers only the public surface.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
