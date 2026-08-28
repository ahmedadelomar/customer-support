# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-management/CS-202-categories-priorities/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 2 — Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-202-categories-priorities`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `tickets, configuration`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Categories and priorities
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support manager,
I want to configure the category tree and the priority scale,
So that tickets are classified the way our business actually works and SLA targets can differ by urgency.

Scope:
- A nested category tree with bilingual names and a materialised path for fast subtree queries.
- Per-category defaults: priority, department and SLA policy, applied at ticket creation.
- Categories hidden from the customer portal for agent-only classifications.
- A configurable priority scale with a level, colour and a single default.
- Reordering and deactivating without breaking existing tickets.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an administrator,
When I open Administration > Categories,
Then I see the category tree and can create, rename, move, reorder and deactivate nodes.

Given I create a child category,
Then its materialised path and depth are computed from its parent.

Given I move a category to a new parent,
Then the paths of the node and all its descendants are recomputed in one transaction.

Given a category has defaults set,
When a ticket is created in it,
Then the default priority, department and SLA policy are applied unless explicitly overridden.

Given a category is marked hidden from the portal,
Then customers cannot select it when submitting a ticket,
But agents can still classify into it.

Given I deactivate a category that is used by open tickets,
Then existing tickets keep it and continue to display it,
But it can no longer be selected for new tickets.

Given the priority scale,
Then each priority has a code, bilingual name, numeric level, colour and active flag,
And exactly one is the default.

Given I try to delete a priority referenced by an SLA target or a ticket,
Then the request is refused, and I am directed to deactivate it instead.

Given category and priority names, they render in the active language everywhere they appear.
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

- **Blocked by / related ids:** CS-201-create-track-tickets
- **Depends on code areas or other stories:**

- `TicketCategory`, `TicketPriority` entities — already defined, with `Path` and `Depth` on the category.
- `DbSeeder` seeds five categories and four priorities.
- CS-501, which reads priority to select SLA targets.

## Extra notes (optional)

- The materialised `Path` column is what makes "all tickets in this subtree" a single index seek rather than a recursive query.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Moving a subtree is the operation most likely to corrupt the tree. Recompute descendants with a single `LIKE` update on the old path prefix inside a transaction.

## Out of scope

- What this story explicitly does **not** cover:

- AI-driven categorisation — CS-703.
- Per-category custom fields.
