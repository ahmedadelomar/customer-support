# Story 06 — Responsive layout and accessibility (Story: CS-1202-responsive-web-mobile)

## Prerequisites

- CS-1201 must land first: RTL and responsiveness interact, and fixing them separately means fixing the same components twice.
- `DataTableComponent` and `AgentShellPage` already implement the responsive patterns. This story audits and completes them.

## Story Goal

Every screen works at 360px, on a tablet, and on a desktop, in both directions, and is usable with a
keyboard and a screen reader. The portal is installable to a home screen.

## Context — Read These Files First

1. `.squad/stories/platform/CS-1202-responsive-web-mobile/intake.md`.
2. [frontend/src/app/shared/ui/data-table/data-table.component.html](frontend/src/app/shared/ui/data-table/data-table.component.html) — the desktop table plus mobile card pattern, and the `overflow-x-auto` wrapper.
3. [frontend/src/app/layout/agent-shell/agent-shell.page.html](frontend/src/app/layout/agent-shell/agent-shell.page.html) — the drawer, backdrop and RTL-aware translate classes.
4. [frontend/src/styles.scss](frontend/src/styles.scss) — the shared `.btn`, `.form-control` and `.card` component classes.

## Product rules (from story)

- **No horizontal page scroll at any width.** Wide content scrolls inside its own `overflow-x-auto` container.
- **Touch targets are at least 44×44px.** The current `px-3 py-1.5` buttons are below this and must be enlarged on touch viewports.
- **Mobile shows the same data as desktop**, reorganised — not a reduced subset.
- **Every icon-only button needs an accessible name** via `aria-label`.
- **Focus is always visible.** Never remove the focus ring without replacing it.
- **Breakpoints:** `md` (768px) switches tables to cards; `lg` (1024px) pins the sidebar.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

_No changes required in this layer for this story._

## Frontend Tasks

### 1 — Responsive audit of every screen

Walk each implemented route at 360px, 768px and 1280px, in both directions, and fix:

- Filter bars that overflow: they must wrap (`flex-wrap`) with each control at `min-w-*` plus `flex-1`.
- Page headers whose action buttons collide with the title.
- Forms: every `grid md:grid-cols-2` must collapse to one column below `md`.
- Modals and side panels: full-screen below `md`.
- The data-table action menu: near the viewport edge it must not be clipped. Currently it is absolutely positioned with `end-0`; verify it stays on screen inside the `overflow-x-auto` wrapper, and switch to a fixed-position portal if it does not.

### 2 — Touch targets and mobile spacing

**File:** [frontend/src/styles.scss](frontend/src/styles.scss)

Raise the minimum hit area on touch devices:

```scss
@layer components {
  .btn {
    @apply inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm
           font-medium transition disabled:cursor-not-allowed disabled:opacity-50;
    min-height: 2.75rem; /* 44px — the minimum comfortable touch target */
  }
}

@media (pointer: coarse) {
  .btn-secondary,
  .btn-primary {
    @apply px-4 py-2.5;
  }
}
```

Apply the same minimum to the row action buttons in the data table, which are currently `p-1.5`.

### 3 — Accessibility pass

Per screen:

- Icon-only buttons get `aria-label` bound to a translation key. The data table and shell already do this; new screens must too.
- Tables use `<th scope="col">` and `aria-sort` on sortable headers — already implemented in `data-table.component.html`; keep it when adding columns.
- Every form control has an associated `<label for>`.
- The mobile drawer traps focus while open and returns focus to the menu button on close.
- The toast region is `role="status" aria-live="polite"` — already correct in `app.ts`.
- Error summaries on forms are announced: add `role="alert"` to the validation summary.
- Colour is never the only signal: status chips carry a text label as well as a colour, which the current `StatusChipConfig` already enforces through `labelKey`.

### 4 — PWA manifest

**File:** `frontend/public/manifest.webmanifest`, [frontend/src/index.html](frontend/src/index.html)

Add a manifest with the product name in both languages, `start_url: "/portal"`, `display: "standalone"`, theme colours matching the brand tokens, and 192px and 512px icons. Link it from `index.html` and add `<meta name="theme-color">`.

Note that the branding story (CS-1205) will make the manifest values dynamic per branch; keep the static file as the fallback.

## Verification Steps

1. Open every implemented route at 360px in Chrome device emulation, in both Arabic and English: no horizontal page scroll anywhere.
2. Confirm lists render as cards below 768px and as a table above, with the same fields.
3. Confirm the row action menu is fully visible when opened on the last row and at the viewport edge.
4. Tab through each screen: focus order is logical, focus is always visible, and the drawer traps focus while open.
5. Run Lighthouse accessibility on the list, form and detail pages: score 95 or above with no critical issues.
6. Verify with a screen reader that icon-only buttons announce a meaningful name.
7. Confirm all interactive controls measure at least 44px on a touch viewport.
8. Install the portal to a home screen on Android and confirm the icon and name are correct.

## Done Criteria

- [ ] Every implemented screen passes the 360px, 768px and 1280px audit in both directions.
- [ ] No horizontal page scrolling at any width.
- [ ] Touch targets meet 44px on coarse pointers.
- [ ] Lighthouse accessibility is 95+ on the main screen types.
- [ ] The drawer traps and restores focus.
- [ ] The PWA manifest is present and the portal is installable.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
