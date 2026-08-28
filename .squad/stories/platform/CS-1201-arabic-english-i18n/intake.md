# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/platform/CS-1201-arabic-english-i18n/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 12 — Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1201-arabic-english-i18n`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `platform, foundation, i18n`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Arabic & English
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As any user of the system,
I want the entire product in Arabic or English with correct right-to-left layout,
So that Arabic-speaking staff and customers can use it as a first-class language rather than a translation afterthought.

Scope:
- Every user-visible string in both languages, with no hard-coded text in components.
- Document direction driven by the active language, so the whole UI mirrors without a second stylesheet.
- Bilingual *data*, not just UI chrome: category names, statuses, KB articles and notification text are stored in both languages.
- Arabic-aware dates, numbers and calendars.
- The chosen language travels with the request, so validation messages, notifications and exported reports come back in the right language.
- The customer's preferred language drives outbound email, SMS and WhatsApp regardless of the agent's own language.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I switch the language,
Then every label, button, table header, validation message and empty state changes language,
And the document direction flips between rtl and ltr,
And my choice persists across reloads and sessions.

Given the app loads for the first time,
Then Arabic is the default,
And the direction is correct on first paint, with no visible flash of the wrong layout.

Given a data record with bilingual fields (a ticket category, a status, a KB article),
Then the value shown matches the active language,
And falls back to the other language when the active one is empty rather than showing a blank.

Given a request to the API,
Then the active language is sent as Accept-Language,
And validation errors and problem-details come back in that language.

Given a date or number is displayed,
Then it is formatted for the active locale (ar-SA or en-US).

Given a customer whose preferred language is Arabic,
When any outbound email, SMS or WhatsApp message is sent,
Then it uses Arabic regardless of which language the agent was working in.

Given a translation key is missing in the active language,
Then the fallback language value is shown rather than the raw key.
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

- `@ngx-translate/core` and `@ngx-translate/http-loader` are already installed.
- `LanguageService` in `frontend/src/app/core/services/language.service.ts` — already implemented, including direction handling and the SSR guard.
- `languageInterceptor` — already sends `Accept-Language`.
- `LocalizedText` owned value object and its `AppDbContext` convention — already implemented server-side.
- `AddRequestLocalization` in `Program.cs` — already configured with `ar` as the default culture.

## Extra notes (optional)

- Most of the plumbing already exists in the skeleton. The work is completing the translation files, adding the Arabic font, and making the server localise its own messages.
- Tajawal is referenced in the Tailwind font stack but is not yet loaded.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Use CSS logical properties (`ps-*`, `pe-*`, `start-*`, `end-*`) throughout. A component that uses `pl-4` will not mirror.
- Arabic numerals: decide once whether `ar-SA` should render Eastern-Arabic digits, and apply that decision everywhere. Mixed digit styles look broken.

## Out of scope

- What this story explicitly does **not** cover:

- Languages beyond Arabic and English.
- Machine translation of ticket content — see CS-702.
- Per-user timezone display, which is CS-1202 territory.
