# Story 54 — Messaging provider configuration, failover and sandbox (Story: CS-1103-messaging-providers)

## Prerequisites

- CS-301, CS-302 and CS-304 must be complete — this configures the providers they send through.
- CS-504 provides the outbox queued messages sit in.

## Story Goal

One place to configure, monitor and fail over the email, SMS and WhatsApp providers — plus a sandbox
mode that makes it impossible for a test environment to message real customers.

## Context — Read These Files First

1. `.squad/stories/integrations/CS-1103-messaging-providers/intake.md`.
2. [backend/src/CustomerSupport.Domain/Integrations/IntegrationConnection.cs](backend/src/CustomerSupport.Domain/Integrations/IntegrationConnection.cs) — `IntegrationType` already distinguishes the provider kinds.
3. [backend/src/CustomerSupport.Domain/Channels/MessageDeliveryLog.cs](backend/src/CustomerSupport.Domain/Channels/MessageDeliveryLog.cs) — `ProviderName` records which provider actually sent.
4. [backend/src/CustomerSupport.Domain/Channels/ChannelAccount.cs](backend/src/CustomerSupport.Domain/Channels/ChannelAccount.cs) — `IntegrationConnectionId` links an account to its provider.

## Product rules (from story)

- **Sandbox mode records without sending**, and the UI states it prominently everywhere a message can be composed. This is the guard against a staging environment messaging real customers.
- **Failover is automatic to a configured secondary** and always logged, including which provider actually sent.
- **Webhook signatures are verified before processing**, on every provider.
- **Deactivating a provider must not lose queued messages** — they route to the fallback or wait.
- **Cost is tracked per provider** so the choice can be evaluated on evidence.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Provider abstraction with failover

One interface per message type (`IEmailSender`, `ISmsSender`, `IWhatsAppSender`), with adapters resolved from `IntegrationConnection.Provider`.

A dispatcher wraps them:

```csharp
var providers = await GetOrderedProvidersAsync(type, branchId, ct);   // primary, then fallbacks

foreach (var provider in providers)
{
    if (provider.Status == IntegrationStatus.Degraded && !IsProbeDue(provider)) continue;

    try
    {
        var result = await adapters[provider.Provider].SendAsync(message, provider, ct);
        await RecordSuccessAsync(provider, result, ct);
        return result;
    }
    catch (Exception ex)
    {
        await RecordFailureAsync(provider, ex, ct);
        logger.LogWarning(ex, "Provider {Provider} failed; trying the next", provider.Name);
    }
}

// Nothing worked. Leave it in the outbox to retry rather than dropping it.
throw new IntegrationUnavailableException($"No {type} provider is available.");
```

Throwing rather than swallowing is deliberate: the outbox retries, so a total provider outage delays messages rather than losing them.

### 2 — Sandbox mode

A per-connection `SandboxMode` flag in `SettingsJson`, plus a global `messaging.sandboxMode` setting that overrides all of them.

When enabled, the dispatcher records a `MessageDeliveryLog` row with `ProviderName = "sandbox"` and status `Sent`, and does not call the provider.

Expose the state on `GET /api/settings/public` so the UI can show it before sign-in, and log a warning at startup when sandbox is on in a Production environment — that combination is almost always a mistake in one direction or the other.

### 3 — Health monitoring and statistics

A periodic health-check job calling each active connection's test method, updating status and alerting administrators on transition to degraded — alert on the transition, not on every failed check, or the alert becomes noise.

`GET /api/integrations/messaging/statistics` aggregates `MessageDeliveryLog` by provider: sent, delivered, failed, delivery rate, and estimated cost from the recorded segments (CS-304) or per-message rate.

## Frontend Tasks

### 4 — Provider administration and sandbox banner

A providers screen grouped by type, each card showing name, provider, status badge, last health check and a failover-order indicator, with **Test** and **Set as primary** actions.

The form covers provider, credentials (write-only), sandbox toggle, and provider-specific settings rendered from a per-provider schema.

**A persistent banner across the whole application when sandbox mode is on** — top bar, ticket composer and portal — reading "Sandbox mode: messages are recorded but not sent". A quiet setting buried in an admin screen is how someone eventually believes a test message went out.

A statistics section with per-provider volume, delivery rate and cost.

## Verification Steps

1. Configure an email provider and test it: a test message is sent and the result reported.
2. Send a reply: it goes through the configured provider and the delivery log records the provider name.
3. Break the primary and configure a secondary: the message is sent by the secondary and the failover is logged.
4. Break both: the message stays in the outbox and retries rather than being lost.
5. Enable sandbox: a reply is recorded with `ProviderName = "sandbox"` and nothing is sent.
6. Confirm the sandbox banner is visible in the agent workspace, the ticket composer and the portal.
7. Start the API in Production with sandbox on: a startup warning is logged.
8. Fail a provider repeatedly: it is marked degraded and administrators are alerted once on the transition, not on every check.
9. Restore it: the status returns to connected on the next health check.
10. Post a provider webhook with an invalid signature: rejected.
11. Deactivate a provider with messages queued: they route to the fallback or wait, and none are lost.
12. Open the statistics view: per-provider volume, delivery rate and cost are populated.

## Done Criteria

- [ ] One abstraction per message type with adapters resolved by provider key.
- [ ] Automatic failover to configured secondaries, always logged with the sending provider.
- [ ] Total outage leaves messages in the outbox rather than dropping them.
- [ ] Sandbox mode records without sending and is impossible to miss in the UI.
- [ ] Health monitoring alerts on transition, not on every failure.
- [ ] Webhook signatures are verified on every provider.
- [ ] Per-provider statistics and cost are reported.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
