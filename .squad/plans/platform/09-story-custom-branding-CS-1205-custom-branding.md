# Story 09 — Runtime branding and theming (Story: CS-1205-custom-branding)

## Prerequisites

- CS-1204 must be complete: branding resolves per branch.
- The `brand-*` and `accent-*` Tailwind colours already read from CSS custom properties, so runtime theming needs only a service that writes them.

## Story Goal

An organisation applies its own identity — name, logo, colours, email header and footer — per branch,
at runtime, with no rebuild. Colour values are validated before they reach a stylesheet, and portal CSS is
sanitised and scoped.

## Context — Read These Files First

1. `.squad/stories/platform/CS-1205-custom-branding/intake.md`.
2. [backend/src/CustomerSupport.Domain/Organization/BrandingSetting.cs](backend/src/CustomerSupport.Domain/Organization/BrandingSetting.cs) — a null `BranchId` is the global default.
3. [frontend/src/styles.scss](frontend/src/styles.scss) — the `:root` custom properties that are the theming hook.
4. [frontend/tailwind.config.js](frontend/tailwind.config.js) — `brand.*` and `accent.*` already resolve from those variables with hard-coded fallbacks.
5. CS-104 for the attachment storage the logo upload reuses.

## Product rules (from story)

- **Resolution:** the branch's branding row, else the global row, else the shipped defaults.
- **Colours are validated as `^#[0-9A-Fa-f]{6}$` before being written into a stylesheet.** An unvalidated string passed to `style.setProperty` is a CSS injection vector.
- **Logos are stored through attachment storage**, not as data URLs in the database, and are referenced by absolute URL in email so clients can load them.
- **Portal custom CSS is sanitised and scoped** to a portal root class, and can never affect the agent workspace.
- **Branding is public read.** The portal needs it before anyone signs in, so the read endpoint is anonymous while writes require `admin.branding.manage`.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/branding` | anonymous | Resolved branding for the requested branch or the global default. |
| PUT | `/api/branding` | `admin.branding.manage` | Body includes an optional `branchId`. |
| POST | `/api/branding/logo` | `admin.branding.manage` | Multipart upload; returns the stored URL. |
| DELETE | `/api/branding/override` | `admin.branding.manage` | Removes a branch override. |

## Backend Tasks

### 1 — Branding query and command

**File:** `backend/src/CustomerSupport.Application/Branding/`

`GetBrandingQuery` resolves branch then global then defaults and is marked `[AllowAnonymous]` at the controller. Cache it in memory keyed by branch, invalidated on write — it is read on every page load by every visitor.

`UpdateBrandingCommand` validates each colour:

```csharp
RuleFor(x => x.PrimaryColor).Matches("^#[0-9A-Fa-f]{6}$")
    .WithMessage("Colour must be a 6-digit hex value such as #5B2C8D.");
```

Apply the same rule to secondary and accent.

### 2 — Logo upload and CSS sanitisation

The logo endpoint accepts `image/png`, `image/jpeg` and `image/svg+xml` up to 2MB, stores through `IFileStorage`, and returns the URL.

**SVG uploads are an XSS vector** — an SVG can carry a `<script>` element. Either strip scripts and event handlers server-side, or refuse SVG and accept raster formats only. Refusing SVG is the safer default; say so in the UI.

For `PortalCustomCss`, strip `@import`, `expression(`, `javascript:` and `</style` before storing, and wrap the stored value in a scoping selector when serving it.

### 3 — Branded email templates

Add an `IEmailTemplateRenderer` that wraps outbound message bodies in the branch's `EmailHeaderHtml` and `EmailFooterHtml`, substituting the logo as an absolute URL and the brand colour into inline styles — email clients do not support CSS custom properties, so the colour must be inlined at render time.

## Frontend Tasks

### 4 — Branding service

**File:** `frontend/src/app/core/services/branding.service.ts`

Fetch `/api/branding` during app initialisation, before the first render, and write the CSS variables:

```ts
apply(branding: Branding): void {
  const root = this.#document.documentElement;
  // Validate again on the client: never write an unvalidated value into a style property.
  const hex = /^#[0-9A-Fa-f]{6}$/;
  if (hex.test(branding.primaryColor)) {
    root.style.setProperty('--brand-700', branding.primaryColor);
    root.style.setProperty('--brand-600', this.#lighten(branding.primaryColor, 0.1));
    root.style.setProperty('--brand-500', this.#lighten(branding.primaryColor, 0.2));
    root.style.setProperty('--brand-100', this.#lighten(branding.primaryColor, 0.85));
    root.style.setProperty('--brand-50', this.#lighten(branding.primaryColor, 0.94));
  }
  ...
}
```

Also set the favicon and `document.title` from the resolved product name in the active language.

Register it in `provideAppInitializer` next to `LanguageService.initialise()` so branding is applied before first paint and the page never flashes the default purple.

### 5 — Branding admin screen with live preview

**File:** `frontend/src/app/features/admin/branding/branding.page.ts`

Form fields per the entity, with colour inputs backed by `<input type="color">` plus a hex text field kept in sync.

The preview pane renders a miniature of the shell — sidebar, a primary button, a status chip, a card — using the draft values applied to a scoped element rather than `:root`, so previewing does not restyle the admin screen itself.

Include a branch selector for users with multiple branches, and a **Remove override** action that reverts to the global default.

## Verification Steps

1. Change the primary colour and confirm the preview updates before saving.
2. Save, reload, and confirm the workspace and the portal both reflect the new colour with no rebuild.
3. Confirm the favicon and browser tab title change to the branded product name.
4. Submit `red; background: url(evil)` as a colour: rejected with a field-level error, and nothing is written to any stylesheet.
5. Upload a PNG logo and confirm it appears in the sidebar, on the portal, and in an outbound email as an absolute URL.
6. Attempt to upload an SVG containing a `<script>` element: refused (or the script is stripped, if that route was chosen).
7. Set branch-specific branding: users in that branch see it, others see the global default.
8. Remove the override and confirm the branch reverts to the global default.
9. Enter portal CSS containing `@import` and confirm it is stripped, and that the agent workspace is unaffected.
10. Hard-refresh: branding is applied on first paint, with no flash of the default theme.

## Done Criteria

- [ ] Branding resolves branch, then global, then defaults, and the read endpoint is anonymous and cached.
- [ ] Colours are validated server-side and again client-side before being written to a style property.
- [ ] Logo upload goes through attachment storage; SVG is refused or sanitised.
- [ ] Portal custom CSS is sanitised and scoped, and cannot reach the agent workspace.
- [ ] Outbound email carries the branded header, footer and inlined brand colour.
- [ ] Branding is applied during app initialisation with no flash of the default theme.
- [ ] The admin screen has a working live preview and branch override support.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
