# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/security-administration/CS-1004-system-configuration/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 10 — Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1004-system-configuration`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `administration, foundation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
System configuration
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a system administrator,
I want to change operational settings from a screen rather than a deployment,
So that the support team can adapt the system without waiting on a release.

Scope:
- A typed key/value settings store, grouped by category, editable in the admin UI.
- Branch-level overrides of global values, because branches genuinely differ (working hours, sender addresses).
- Secret settings encrypted at rest and write-only through the API.
- A typed settings reader for server code, cached with invalidation on write.
- Seeded system settings that can be edited but not deleted.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open Administration > Settings,
Then settings are grouped by category with the appropriate editor per data type (string, int, bool, json, secret).

Given a setting is marked IsSecret,
Then its value is encrypted at rest,
And the API returns a masked placeholder rather than the value,
And submitting the unchanged placeholder leaves the stored value untouched.

Given a branch-scoped row exists for a key,
Then it overrides the global row for users acting in that branch,
And the UI shows both the inherited value and the override.

Given I change a setting,
Then the change is audited,
And the cached value is invalidated so the next read returns the new value without a restart.

Given a setting is marked IsSystem,
Then I can edit it but the delete action is unavailable.

Given I enter a value that does not parse as the declared data type,
Then the request is rejected with a field-level error.

Given code reads a setting that has no row,
Then a documented default is returned rather than a null reference.
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

- `SystemSetting` entity in `backend/src/CustomerSupport.Domain/Identity/SystemSetting.cs`.
- ASP.NET Core Data Protection for encrypting secret values.
- CS-1204 (multi-branch) for the branch override semantics — the column exists, the resolution rule is shared.

## Extra notes (optional)

- Settings that other stories depend on: `audit.retentionDays`, `tickets.numberPrefix`, `sla.defaultPolicyId`, `ai.enabled`, `portal.enabled`, `attachments.maxBytes`.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Cache with `IMemoryCache` keyed by branch plus key, and clear the entry on write. A distributed cache is only needed once the API runs multi-instance.

## Out of scope

- What this story explicitly does **not** cover:

- Feature-flag rollout percentages.
- Configuration import and export.
- Branding settings — that is CS-1205, which has its own entity.
