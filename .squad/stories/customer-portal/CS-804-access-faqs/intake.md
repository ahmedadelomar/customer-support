# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-portal/CS-804-access-faqs/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 8 — Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-804-access-faqs`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `portal, self-service`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Access FAQs
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to find answers myself before contacting support,
So that I solve simple problems immediately.

Scope:
- A public help centre with browsable categories.
- FAQ and article pages, no sign-in required.
- Search with suggestions.
- Article feedback.
- Deflection: suggesting relevant articles while a customer types a ticket subject.
- A featured and most-viewed section on the help centre home.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I visit the help centre without signing in,
Then I can browse categories and read published public articles.

Given the help centre home,
Then featured articles and the most viewed appear,
And categories show their article counts.

Given I search,
Then results include title, snippet and category,
And I can filter by category and type.

Given I read an article,
Then I can vote helpful or not helpful,
And a not-helpful vote invites an optional comment.

Given an article does not answer my question,
Then I am offered a clear path to submit a ticket,
With the article referenced on the resulting ticket so authors can see which articles failed.

Given I am typing a ticket subject,
Then relevant articles are suggested beside the form as I type,
And selecting one lets me read it and abandon the ticket if it answers me.

Given a deflection,
Then it is recorded so the value of the help centre is measurable.

Given the help centre in Arabic,
Then it is fully translated with correct direction, and articles appear in Arabic where available.
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

- **Blocked by / related ids:** CS-604-kb-search
- **Depends on code areas or other stories:**

- CS-601 to CS-604 for knowledge base content and search.
- CS-801 for the portal shell.
- CS-1205 for branding.

## Extra notes (optional)

- Deflection measurement is what justifies continued investment in the knowledge base; without it, article writing looks like unmeasured effort.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Search-as-you-type on the ticket form reuses the existing suggestion endpoint with source `Portal`.

## Out of scope

- What this story explicitly does **not** cover:

- Community forums.
- Article comments.
