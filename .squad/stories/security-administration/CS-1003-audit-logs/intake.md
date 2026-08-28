# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/security-administration/CS-1003-audit-logs/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 10 — Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1003-audit-logs`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `security, compliance`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Audit logs
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a system administrator or auditor,
I want an immutable record of who changed what and when,
So that I can investigate incidents and satisfy compliance reviews.

Scope:
- An append-only AuditLog table written by an EF Core interceptor, so no handler can forget to log.
- Coverage for security and configuration entities plus customer and ticket records — deliberately not every table, or the trail becomes unusable.
- Explicit entries for sign-in, failed sign-in, sign-out, permission changes and data exports.
- A searchable viewer with filters for date range, actor, entity type, entity id and action.
- Secret values redacted before they are written.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given any audited entity is created, updated or deleted,
Then an AuditLog row records the actor, action, entity type, entity id, changed values before and after, IP address, user agent, correlation id and timestamp.

Given a property is on the redaction list (password hashes, client secrets, encrypted credentials, tokens),
Then its value never appears in OldValues or NewValues.

Given an update changed only audit columns or redacted fields,
Then no audit row is written, because a row with no meaningful diff is noise.

Given a soft delete,
Then the action is recorded as Delete rather than Update.

Given I open Administration > Audit log,
Then I can filter by date range, actor, entity type, entity id and action,
And results are paged and sorted newest first.

Given I view an entry,
Then I see a readable before-and-after diff of the changed fields only.

Given any user, including a system administrator,
Then no API allows editing or deleting an audit row.

Given the audit log grows,
Then a retention job archives entries older than the configured window rather than letting the table grow without bound.
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

- **Blocked by / related ids:** CS-1001-users-roles
- **Depends on code areas or other stories:**

- `AuditLogInterceptor` in `backend/src/CustomerSupport.Infrastructure/Persistence/Interceptors/` — already implemented; this story adds the viewer, the explicit auth events and retention.
- `AuditLog` entity and its indexes.
- `IAuditContextAccessor`, implemented by `AuditContextAccessor` in the API layer.

## Extra notes (optional)

- Retention window belongs in SystemSetting (`audit.retentionDays`) so it can be changed without a deployment.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- The interceptor already exists and covers entity mutations. What is missing is the read API, the Angular viewer, the explicit authentication events, and the retention job.

## Out of scope

- What this story explicitly does **not** cover:

- Streaming audit events to an external SIEM — a later Integrations story.
- Tamper-evident hash chaining.
