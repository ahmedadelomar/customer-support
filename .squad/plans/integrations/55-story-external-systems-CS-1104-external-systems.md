# Story 55 — Outbound webhooks (Story: CS-1104-external-systems)

## Prerequisites

- CS-504 must be complete — dispatch goes through its outbox.
- `Webhook` and `WebhookDelivery` already exist with retry, backoff and failure-count columns.

## Story Goal

External systems learn about CRM events without polling: signed, retried, replayable deliveries that
never fire for a transaction that rolled back and never expose internal content.

## Context — Read These Files First

1. `.squad/stories/integrations/CS-1104-external-systems/intake.md`.
2. [backend/src/CustomerSupport.Domain/Integrations/Webhook.cs](backend/src/CustomerSupport.Domain/Integrations/Webhook.cs) — `Secret`, `Events`, `MaxRetries`, `ConsecutiveFailureCount`.
3. [backend/src/CustomerSupport.Domain/Integrations/WebhookDelivery.cs](backend/src/CustomerSupport.Domain/Integrations/WebhookDelivery.cs) — `EventId` is the receiver's deduplication key; `NextRetryAt` drives backoff.
4. [backend/src/CustomerSupport.Domain/Integrations/OutboxMessage.cs](backend/src/CustomerSupport.Domain/Integrations/OutboxMessage.cs) — read the class remark on why dispatch goes through here.
5. [backend/src/CustomerSupport.Infrastructure/Jobs/OutboxDispatcherJob.cs](backend/src/CustomerSupport.Infrastructure/Jobs/OutboxDispatcherJob.cs) (CS-504) — the claim-then-send pattern to reuse.

## Product rules (from story)

- **Events are written to the outbox in the same transaction as the state change.** This is what makes "no webhook for a rolled-back transaction" true rather than aspirational.
- **Payloads are signed with HMAC-SHA256** over the raw body, with a timestamp to prevent replay.
- **Every delivery carries a unique `EventId`** so receivers can deduplicate.
- **Backoff is exponential**, then abandonment — a dead endpoint must not consume the queue forever.
- **Persistent failure auto-disables the webhook** and notifies administrators.
- **Payloads are a public contract**: versioned, and never a reused internal DTO.
- **Payloads never contain internal notes, mentions or agent-only data.**

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET/POST/PUT/DELETE | `/api/webhooks` | `integrations.webhooks.manage` | |
| POST | `/api/webhooks/{id}/test` | `integrations.webhooks.manage` | Sends a sample payload. |
| GET | `/api/webhooks/{id}/deliveries` | `integrations.webhooks.manage` | Paged delivery log. |
| POST | `/api/webhooks/deliveries/{id}/replay` | `integrations.webhooks.manage` | |
| GET | `/api/webhooks/events` | `integrations.webhooks.manage` | Subscribable events with example payloads. |

## Backend Tasks

### 1 — Event publication through the outbox

```csharp
public interface IWebhookPublisher
{
    /// <summary>Adds the event to the current unit of work. The caller saves.</summary>
    void Publish(string eventType, object payload, Guid? aggregateId = null);
}
```

Adding to the change tracker rather than saving is the whole point: the event commits with the state change, or not at all.

Supported events: `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.status_changed`, `ticket.resolved`, `ticket.closed`, `ticket.message_added` (customer-visible only), `customer.created`, `customer.updated`, `sla.breached`, `csat.responded`.

Call it from the corresponding command handlers, before their existing `SaveChangesAsync`.

### 2 — Signed delivery with backoff

The dispatcher fans one outbox row out to every active webhook subscribed to that event, creating a `WebhookDelivery` per target.

```csharp
var body = JsonSerializer.Serialize(new WebhookEnvelope
{
    Id = delivery.EventId, Type = eventType, Version = "1",
    CreatedAt = clock.UtcNow, Data = payload,
});

var timestamp = clock.UtcNow.ToUnixTimeSeconds();

// Sign timestamp + body so a captured payload cannot be replayed later.
var signature = Convert.ToHexString(HMACSHA256.HashData(
    Encoding.UTF8.GetBytes(webhook.Secret),
    Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

request.Headers.Add("X-CS-Signature", $"t={timestamp},v1={signature}");
request.Headers.Add("X-CS-Event-Id", delivery.EventId);
request.Headers.Add("X-CS-Event-Type", eventType);
```

On a non-2xx or a timeout: record the status and body (truncated), increment `Attempt`, and set `NextRetryAt` on a 1m, 5m, 30m, 2h, 6h schedule. After `MaxRetries`, mark `Abandoned`.

Increment `ConsecutiveFailureCount` on the webhook; at the threshold, set `IsActive = false` and notify administrators. Reset the counter on any success.

Document the signature verification recipe in the API docs — a signing scheme nobody can verify is decoration.

### 3 — Public payload contracts

Define webhook payload DTOs under `Api/Webhooks/Payloads/`, separate from both internal and public API DTOs, and version them in the envelope.

For `ticket.message_added`, filter internal notes at construction:

```csharp
// Internal notes must never leave the building.
if (message.IsInternalNote) return;
```

Add a test asserting that no webhook payload type contains a property named for internal content, so a future field addition cannot leak by accident.

## Frontend Tasks

### 4 — Webhook administration and delivery log

A list showing name, URL, subscribed events, active state and a health indicator from `ConsecutiveFailureCount`, with an auto-disabled banner where applicable and a **Reactivate** action.

The form has a URL field, an event multi-select with example payloads shown inline, and the generated secret displayed once with the verification recipe.

**Send test** posts a sample and shows the response inline.

The delivery log lists attempt, status code, duration and timestamp, with the request and response bodies expandable and a **Replay** action on failures. This is the screen a partner's developer will ask for when their integration is not working, so it needs the raw bodies, not a summary.

## Verification Steps

1. Create a webhook subscribed to `ticket.created`, then create a ticket: the payload is delivered.
2. Verify the signature at the receiver using the documented recipe.
3. Confirm the `X-CS-Event-Id` header is unique per delivery.
4. Create a ticket inside a transaction that then rolls back: no webhook is delivered.
5. Return 500 from the receiver: retries follow the backoff schedule and are then abandoned.
6. Fail consecutively past the threshold: the webhook auto-disables and administrators are notified.
7. Reactivate it and confirm deliveries resume and the counter resets.
8. Replay a failed delivery: it is re-sent with the same event id.
9. Add an internal note: no `ticket.message_added` webhook fires.
10. Add a customer-visible reply: the webhook fires and the payload contains no internal fields.
11. Send a test payload: the response is shown inline.
12. Inspect the delivery log: request and response bodies are visible for troubleshooting.
13. Run the payload-contract test: it fails if an internal field is added to a payload DTO.

## Done Criteria

- [ ] Events publish into the caller's unit of work, so a rollback delivers nothing.
- [ ] Payloads are HMAC-signed with a timestamp and carry a unique event id.
- [ ] Exponential backoff, abandonment, auto-disable and reactivation all work.
- [ ] The delivery log shows raw request and response bodies and supports replay.
- [ ] Payload DTOs are separate, versioned, and provably free of internal content.
- [ ] The signature verification recipe is documented.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
