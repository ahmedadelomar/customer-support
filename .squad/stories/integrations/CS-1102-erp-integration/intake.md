# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/integrations/CS-1102-erp-integration/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 11 — Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1102-erp-integration`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `integrations, erp`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
ERP
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an organisation,
I want customer and transaction data synchronised with our ERP,
So that agents see accurate account context without switching systems.

Scope:
- A configurable connection to an ERP with encrypted credentials.
- Inbound customer synchronisation, scheduled and on demand.
- Field mapping between ERP and CRM fields, configurable rather than coded.
- On-demand lookup of orders and invoices displayed on the ticket screen.
- Conflict handling when both sides changed a record.
- Sync logging with per-record error reporting.
- A circuit breaker so an ERP outage does not degrade the CRM.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I configure an ERP connection,
Then credentials are encrypted at rest and never returned by the API,
And a test action reports whether the connection works.

Given a scheduled sync,
Then customers are imported or updated according to the configured field mapping,
And each run records records read, written, skipped and failed.

Given a record fails to sync,
Then the run continues and the failure is recorded with its reason,
Rather than the whole run aborting.

Given an incremental sync,
Then it resumes from the last successful watermark rather than re-reading everything.

Given a record changed on both sides since the last sync,
Then the configured conflict rule applies, and the conflict is logged either way.

Given an agent opens a ticket for a synced customer,
Then ERP context such as recent orders is fetched on demand and displayed,
And a slow or failed ERP call leaves the ticket screen fully usable.

Given the ERP is unavailable repeatedly,
Then the circuit breaker opens, the connection is marked degraded, and administrators are alerted,
And calls stop until it recovers.

Given the sync log, I can see every run with its outcome and drill into failures.
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

- `IntegrationConnection` and `IntegrationSyncLog` entities — already defined, including the failure counter and watermark.
- CS-101 for customers, CS-1004 for encrypted settings.

## Extra notes (optional)

- The CRM must never depend on the ERP being up. Every ERP call is enrichment, and every failure path must leave the agent able to work.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Field mapping as configuration rather than code means a new ERP field does not require a deployment.

## Out of scope

- What this story explicitly does **not** cover:

- Writing tickets back into the ERP.
- Real-time bidirectional synchronisation.
- A specific ERP vendor implementation; this story delivers the framework and one reference adapter.
