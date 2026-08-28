# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/integrations/CS-1103-messaging-providers/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 11 — Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1103-messaging-providers`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `integrations, messaging`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Email, SMS & WhatsApp
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an administrator,
I want messaging providers configured in one place with health monitoring,
So that outbound communication is reliable and failures are visible before customers notice.

Scope:
- Provider configuration for email, SMS and WhatsApp behind one abstraction.
- Encrypted credentials with a test action.
- Health monitoring and alerting.
- Failover to a secondary provider.
- Delivery statistics and cost tracking per provider.
- Sandbox mode for testing without sending to real customers.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I configure a provider,
Then credentials are encrypted,
And a test action sends a test message and reports the result.

Given a provider is active,
Then outbound messages route through it,
And each send records the provider used and the outcome.

Given a provider fails repeatedly,
Then the connection is marked degraded and administrators are alerted.

Given a secondary provider is configured,
When the primary is unavailable,
Then messages route to the secondary and the failover is logged.

Given sandbox mode is enabled,
Then messages are recorded but not actually sent,
And the UI shows clearly that sandbox is on,
So a test environment cannot message real customers.

Given the provider dashboard,
Then I see send volume, delivery rate, failure rate and estimated cost per provider.

Given a provider webhook,
Then its signature is verified before processing.

Given a provider is deactivated while messages are queued,
Then queued messages route to the fallback or wait rather than being lost.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| — | None. |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CS-1101-public-apis
- **Depends on code areas or other stories:**

- CS-301, CS-302 and CS-304 for the channel adapters this configures.
- `IntegrationConnection` and `MessageDeliveryLog` entities — already defined.
- CS-504 for the outbox queued messages sit in.

## Extra notes (optional)

- Sandbox mode is what prevents a staging environment messaging real customers, which is the single most damaging integration mistake.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- One abstraction per message type with adapters behind it means adding a provider is a new class, not a change to the channel code.

## Out of scope

- What this story explicitly does **not** cover:

- Building a mail server or SMS gateway.
- Marketing campaign tooling.
