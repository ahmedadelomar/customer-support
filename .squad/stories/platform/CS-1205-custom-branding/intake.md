# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/platform/CS-1205-custom-branding/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 12 — Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1205-custom-branding`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `platform, ux`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Custom branding
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an organisation,
I want the product to carry our identity across the portal, the agent workspace and outbound email,
So that customers see our brand rather than a generic tool.

Scope:
- Per-branch branding: logo, favicon, product name, colour palette.
- Runtime theming through CSS custom properties, so no rebuild is needed.
- Branded email headers and footers on outbound messages.
- Optional custom CSS for the customer portal.
- A live preview in the admin editor before saving.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an administrator with admin.branding.manage,
When I open Administration > Branding,
Then I can set the product name (both languages), logo, dark logo, favicon, primary, secondary and accent colours, support email and support phone.

Given I change the primary colour,
Then a live preview updates before I save.

Given I save branding,
Then the agent workspace and the portal reflect it on the next load without a rebuild or restart.

Given a branch has its own branding row,
Then users and customers in that branch see it,
And branches without one fall back to the global default.

Given an outbound email,
Then it carries the branded header and footer for the relevant branch,
And the logo is referenced by absolute URL so it renders in email clients.

Given I upload a logo,
Then the file type and size are validated,
And it is stored through the attachment storage rather than as a data URL in the database.

Given a colour value,
Then it is validated as a hex colour before being written into a stylesheet, so it cannot inject CSS.

Given custom portal CSS,
Then it is sanitised and scoped to the portal, and cannot affect the agent workspace.
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

- `BrandingSetting` entity — already defined, with a null `BranchId` meaning the global default.
- CSS custom properties already declared in `frontend/src/styles.scss` and wired into `tailwind.config.js` as `brand-*` and `accent-*`.
- CS-104 for the attachment storage the logo upload reuses.
- CS-1204 for branch resolution.

## Extra notes (optional)

- The Tailwind config already reads brand colours from CSS variables, so runtime theming needs only a service that writes those variables.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Validate colours with a strict `^#[0-9A-Fa-f]{6}$` pattern. Writing an unvalidated string into `style.setProperty` is a CSS injection vector.

## Out of scope

- What this story explicitly does **not** cover:

- A full theme designer with fonts and spacing.
- Per-customer white-labelling.
- Custom domains for the portal.
