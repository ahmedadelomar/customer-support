# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/integrations/CS-1104-external-systems/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 11 — Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1104-external-systems`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `integrations, webhooks`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
External systems
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an organisation,
I want external systems notified when things happen in the CRM,
So that our other tools stay in step without polling.

Scope:
- Outbound webhooks subscribed to CRM events.
- Signed payloads so receivers can verify authenticity.
- Retry with exponential backoff and eventual abandonment.
- A delivery log with replay.
- Automatic disabling of persistently failing endpoints.
- A test action and example payloads.
- Reliable dispatch through the transactional outbox.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I create a webhook,
Then I choose which events it subscribes to,
And a signing secret is generated.

Given a subscribed event occurs,
Then a payload is delivered to the endpoint,
And it carries an HMAC signature and a unique event id.

Given the receiver returns a non-success status or times out,
Then delivery is retried with exponential backoff up to the configured limit,
Then abandoned and recorded as such.

Given a webhook fails consecutively beyond its threshold,
Then it is automatically deactivated and administrators are notified,
So one dead endpoint does not consume the queue indefinitely.

Given the delivery log,
Then I see every attempt with its status code, response and timing,
And I can replay a failed delivery.

Given the same event is delivered twice,
Then the event id lets the receiver deduplicate.

Given a test action,
Then a sample payload is sent and the result shown, without waiting for a real event.

Given an event occurs inside a transaction that is later rolled back,
Then no webhook is delivered, because dispatch goes through the outbox.

Given webhook payloads, they never contain internal notes or data the subscriber should not see.
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

- `Webhook`, `WebhookDelivery` and `OutboxMessage` entities — already defined, including backoff and failure-count columns.
- CS-504 for the outbox dispatcher pattern.

## Extra notes (optional)

- Dispatching through the outbox is what makes "no webhook for a rolled-back transaction" true rather than merely intended.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Webhook payloads are a public contract. Version them, and never widen them by reusing an internal DTO.

## Out of scope

- What this story explicitly does **not** cover:

- Inbound webhooks from external systems, which are covered by the channel stories.
- A webhook marketplace or directory.
