# Story 05 — Bilingual UI and data (Arabic and English) (Story: CS-1201-arabic-english-i18n)

## Prerequisites

- `LanguageService`, `languageInterceptor`, `provideTranslateService` and the `LocalizedText` convention already exist. This story completes them rather than introducing them.
- Read [frontend/src/app/core/services/language.service.ts](frontend/src/app/core/services/language.service.ts) before writing any code — the direction effect and the SSR guard are already solved.

## Story Goal

Arabic and English are equal citizens. Every string is translated, the layout mirrors correctly,
bilingual data renders in the active language with a sensible fallback, and the server localises its own
validation and notification text from the request's language.

Outbound customer messages follow the **customer's** preferred language, not the agent's — this is the rule
most easily got wrong, and getting it wrong sends Arabic customers English email.

## Context — Read These Files First

1. `.squad/stories/platform/CS-1201-arabic-english-i18n/intake.md`.
2. [frontend/src/app/core/services/language.service.ts](frontend/src/app/core/services/language.service.ts) — `current`, `direction`, `locale`, `pick()`. Components depend on `current()` inside `computed` to re-render on switch.
3. [frontend/src/app/features/agent/customers/customer-list.page.ts](frontend/src/app/features/agent/customers/customer-list.page.ts) — see how `columns` reads `this.#language.current()` so headers and action labels re-translate.
4. [frontend/public/i18n/en.json](frontend/public/i18n/en.json) and [ar.json](frontend/public/i18n/ar.json) — the existing key structure to extend.
5. [backend/src/CustomerSupport.Domain/Common/LocalizedText.cs](backend/src/CustomerSupport.Domain/Common/LocalizedText.cs) — the `For(culture)` fallback rule.
6. [backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs](backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs) — `ApplyLocalizedTextConvention` maps every `LocalizedText` to `<Prop>En` / `<Prop>Ar` automatically.
7. [backend/src/CustomerSupport.Api/Program.cs](backend/src/CustomerSupport.Api/Program.cs) — `AddRequestLocalization` with `ar` as the default culture is already registered.

## Product rules (from story)

- **Arabic is the default language** and the default document direction is RTL.
- **No hard-coded user-visible text in components.** Every string goes through a translation key present in *both* files.
- **Fallback, never blank:** an empty Arabic value falls back to English and vice versa. `LocalizedText.For()` already does this; the client `pick()` matches it.
- **Outbound customer messages use `Customer.PreferredLanguage`**, and internal notifications use the recipient agent's `PreferredLanguage`. Never the language of whoever triggered the action.
- **Bilingual data is stored, not translated at render time.** Categories, statuses, KB articles and notification text all carry both languages.
- **Dates and numbers** use `ar-SA` or `en-US` via the `locale` signal. Do not hand-format.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Localise validation and problem-details messages

**File:** `backend/src/CustomerSupport.Api/Infrastructure/GlobalExceptionHandler.cs`, `backend/src/CustomerSupport.Application/Common/Localization/`

Add a resource-backed `IMessageLocalizer` with `ar` and `en` resource files. Replace the literal English titles in `GlobalExceptionHandler` with localized lookups, and give every FluentValidation rule an explicit message key rather than relying on the library defaults, which are English-only.

`AddRequestLocalization` already sets `CultureInfo.CurrentUICulture` from `Accept-Language`, which the client sends on every request via `languageInterceptor`.

### 2 — Expose bilingual pairs, not resolved strings, in list DTOs

List and detail DTOs must return **both** languages (as `NameEn` / `NameAr`) rather than a single resolved string. The client picks, which means one cached API response serves both languages and a language switch needs no refetch.

`CustomerListItemDto` already does this. Follow it everywhere.

The exception is outbound content — email bodies, SMS text, notification payloads — where the server resolves against the **recipient's** language before sending.

### 3 — Arabic-aware search normalisation

**File:** `backend/src/CustomerSupport.Infrastructure/Persistence/`

Arabic search is unusable without normalisation: users type أ, إ, ا interchangeably, and ة for ه.

Set the database collation for searchable text columns to `Arabic_CI_AI` (case- and accent-insensitive) in a migration, and normalise the search term server-side before matching. Add a `NormalizeArabic` helper that folds alef variants, taa marbuta and tatweel, and use it on both the stored normalized column and the query term.

## Frontend Tasks

### 4 — Load the Arabic font

**File:** [frontend/src/index.html](frontend/src/index.html), [frontend/src/styles.scss](frontend/src/styles.scss)

`Tajawal` is referenced in the Tailwind font stack but never loaded. Add the Google Fonts link for Tajawal and Inter in `index.html` with `preconnect`, or self-host the woff2 files under `public/fonts/` and declare `@font-face` — self-hosting avoids a third-party request and is the better default for an internal tool.

Without this, Arabic falls back to a system font and looks visibly different from the design.

### 5 — Complete the translation files

**File:** [frontend/public/i18n/en.json](frontend/public/i18n/en.json), [frontend/public/i18n/ar.json](frontend/public/i18n/ar.json)

Extend both files to cover every namespace the product needs: `tickets.*`, `admin.*`, `kb.*`, `reports.*`, `sla.*`, `channels.*`, `portal.*`, `ai.*`, and the full `enums.*` map for every enum in `core/models/enums.ts`.

Add a build-time check (a small Node script run in `npm test`) asserting the two files have identical key sets. A key present in one file only silently renders as the raw key in the other language, which is the single most common i18n bug.

### 6 — Audit every component for logical properties

Grep the codebase for physical-direction Tailwind classes and replace them:

```
pl-  -> ps-      pr-  -> pe-
ml-  -> ms-      mr-  -> me-
left- -> start-  right- -> end-
text-left -> text-start   text-right -> text-end
border-l -> border-s      border-r -> border-e
```

A component using `pl-4` will not mirror in Arabic. The shared components already comply; verify each new screen against this list before it merges.

### 7 — Language-reactive computed values

Any `computed` that produces translated text must read `this.#language.current()` so it recomputes on switch. `TranslateService.instant()` called outside a language-dependent `computed` returns a value that never updates.

`customer-list.page.ts` demonstrates the correct pattern in its `columns` computed.

## Verification Steps

1. Switch language on every implemented screen: all text changes, and no raw keys appear.
2. Confirm the direction flips and that no element visually escapes its container in RTL.
3. Hard-refresh in Arabic: the first paint is already RTL, with no flash of LTR.
4. Trigger a validation error with `Accept-Language: ar`: the message returns in Arabic.
5. Create a category with an English name and a blank Arabic name: the Arabic UI shows the English value rather than a blank.
6. Set a customer to Arabic, then have an English-speaking agent reply: the outbound message is Arabic.
7. Search for a customer named "احمد" using the spelling "أحمد": normalisation matches both.
8. Delete a key from `ar.json` only: the key-parity check fails.
9. Confirm dates render as `ar-SA` in Arabic and `en-US` in English.

## Done Criteria

- [ ] No hard-coded user-visible strings remain in any component.
- [ ] `en.json` and `ar.json` have identical key sets, enforced by an automated check.
- [ ] The Arabic font is loaded and applied under `[dir="rtl"]`.
- [ ] Server validation and problem-details messages are localised from `Accept-Language`.
- [ ] Outbound customer messages use the customer's preferred language; internal notifications use the recipient's.
- [ ] Arabic search normalisation works for alef and taa-marbuta variants.
- [ ] No physical-direction CSS classes remain.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
