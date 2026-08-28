# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ai-features/CS-701-ticket-summaries/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 7 — AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-701-ticket-summaries`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `ai`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Ticket summaries
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent picking up a long ticket,
I want a short summary of what has happened so far,
So that I can help without reading twenty messages first.

Scope:
- On-demand and automatic summarisation of a ticket conversation.
- A summary in the ticket's language.
- Staleness detection, so a summary of an old conversation is not presented as current.
- Cost and latency tracking per generation.
- Human review: accept, edit or reject.
- A master switch and per-feature spend limits.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a ticket with several messages,
When I request a summary,
Then a concise summary is generated in the ticket's language,
And it covers the customer's issue, what has been tried, and the current state.

Given the summary is generated,
Then the model, token counts, latency and prompt version are recorded.

Given new messages arrive after a summary was generated,
Then the summary is marked stale and the UI offers to regenerate,
Rather than silently showing outdated information.

Given I edit a suggestion before accepting it,
Then the edited text is stored alongside the original,
So the difference between what the model produced and what a human wanted is measurable.

Given I reject a suggestion,
Then I can give a reason, which feeds prompt tuning.

Given the AI master switch is off,
Then no generation occurs and the UI hides AI controls entirely.

Given the daily request limit for the feature is reached,
Then further generations are refused with a clear message rather than silently failing.

Given the model call fails or times out,
Then the ticket screen still works and the failure is logged,
Because AI is an enhancement, not a dependency.

Given a summary, it is advisory: nothing is written to the ticket until an agent accepts it.
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

- CS-201 for tickets and messages.
- `AiSuggestion` and `AiModelConfig` entities — already defined.
- `IAiCompletionService` — declared, not implemented.
- CS-1004 for the `ai.enabled` setting.

## Extra notes (optional)

- Everything in this section must degrade gracefully. A failed AI call should never break a screen an agent needs to do their job.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Use the official Anthropic C# SDK (the `Anthropic` NuGet package), not raw HTTP.
- Current models use adaptive thinking and an effort level; temperature and other sampling parameters are rejected.

## Out of scope

- What this story explicitly does **not** cover:

- Fine-tuning or training on ticket data.
- Summarising across multiple tickets.
