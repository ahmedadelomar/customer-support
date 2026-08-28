# integrations — plan overview

Entry point for the **Section 11 — Integrations** feature. Stories execute in order by their `NN` prefix.

The boundary between this product and everything around it. Two rules run through all four stories:
**the CRM never depends on an external system being up**, and **every external contract is versioned and
separate from the internal one**, so an internal refactor is never a breaking change for a partner.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 52 | [52-story-public-apis-CS-1101-public-apis.md](52-story-public-apis-CS-1101-public-apis.md) | Public API with client credentials and idempotency | CS-1101-public-apis | 14 |
| 53 | [53-story-erp-integration-CS-1102-erp-integration.md](53-story-erp-integration-CS-1102-erp-integration.md) | ERP synchronisation framework | CS-1102-erp-integration | 52 |
| 54 | [54-story-messaging-providers-CS-1103-messaging-providers.md](54-story-messaging-providers-CS-1103-messaging-providers.md) | Messaging provider configuration, failover and sandbox | CS-1103-messaging-providers | 53 |
| 55 | [55-story-external-systems-CS-1104-external-systems.md](55-story-external-systems-CS-1104-external-systems.md) | Outbound webhooks | CS-1104-external-systems | 54 |

## Dependency notes

- **Story 52 establishes the external API surface and the client-credentials model** that the rest assume.
- **The public API is deliberately separate from the internal one.** The Angular app uses `/api/*`; partners use `/api/v1/*` with its own DTOs. Sharing them means every internal change is a partner-visible breaking change.
- Stories 53–55 all use `IntegrationConnection` for encrypted credentials. No integration story stores credentials of its own.
- Story 55 (webhooks) dispatches through the transactional outbox built in CS-504, which is what makes "no webhook for a rolled-back transaction" actually true.
- Story 54 configures the providers that CS-301, CS-302 and CS-304 send through. Those stories can ship with a single hard-configured provider and adopt this later, but the abstraction should be in place from the start.
- **Circuit breakers throughout.** An ERP or provider outage must degrade to a clear message, never to a broken agent screen.
