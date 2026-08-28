# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/platform/CS-1202-responsive-web-mobile/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 12 — Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1202-responsive-web-mobile`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `platform, ux`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Web and mobile friendly
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an agent or a customer,
I want the product to work properly on a phone and a tablet, not just a desktop,
So that I can handle support work away from my desk.

Scope:
- A responsive shell: the sidebar becomes a drawer, the top bar collapses, the content reflows.
- Lists render as a table on desktop and as stacked cards on mobile, showing the same data.
- Forms reflow to a single column, and touch targets meet the 44px minimum.
- No horizontal page scrolling at any width; wide content scrolls inside its own container.
- Accessible keyboard navigation and screen-reader labels.
- A PWA manifest so the portal can be installed to a home screen.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given any screen at 360px width,
Then the page does not scroll horizontally,
And all primary actions are reachable.

Given a list screen on a small viewport,
Then rows render as cards showing the key fields and the row action menu,
And the same data is available as on desktop.

Given a wide table or a code block,
Then it scrolls inside its own container rather than forcing the page body to scroll.

Given the agent shell on a small viewport,
Then the sidebar is hidden behind a menu button,
And opening it shows a backdrop that closes the drawer when tapped.

Given I navigate with a keyboard only,
Then focus order is logical, focus is always visible, and no control is unreachable.

Given a screen reader,
Then tables have proper headers, form controls have labels, and icon-only buttons have accessible names.

Given the customer portal on a phone,
Then it can be installed to the home screen via a web app manifest.

Given right-to-left layout on mobile,
Then the drawer opens from the correct side and all spacing mirrors.
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

- **Blocked by / related ids:** —
- **Depends on code areas or other stories:**

- Tailwind CSS 3.4 — already configured.
- `DataTableComponent` — already renders a desktop table and a mobile card view.
- `AgentShellPage` — already implements the responsive drawer.
- CS-1201 for RTL, since mirroring and responsiveness interact.

## Extra notes (optional)

- The shared components already implement most of this. The story is about auditing every screen against the rules and fixing the gaps, plus adding the PWA manifest and the accessibility pass.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Breakpoints in use: `md` (768px) switches table to cards, `lg` (1024px) pins the sidebar.

## Out of scope

- What this story explicitly does **not** cover:

- A native mobile application.
- Offline support and background sync.
- Push notifications to the device — see CS-504.
