# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ai-features/CS-702-suggested-replies/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 7 — AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-702-suggested-replies`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `ai`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Suggested replies
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want a drafted reply I can edit and send,
So that I answer faster without sending something I have not read.

Scope:
- Draft replies generated from the ticket conversation and relevant knowledge base articles.
- Replies in the customer's language and in the organisation's tone.
- Agent review before anything is sent, always.
- Measurement of accept, edit and reject rates.
- Reuse of the quick reply placeholder vocabulary.
- Grounding in knowledge base content, with the sources cited to the agent.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given an open ticket awaiting a reply,
When I request a suggested reply,
Then a draft is generated in the customer's language,
And relevant knowledge base articles are retrieved and used as grounding,
And the articles used are listed so I can check them.

Given a draft,
Then it is inserted into the composer as editable text,
And nothing is sent automatically under any circumstances.

Given I send the draft unchanged,
Then it is recorded as accepted.

Given I edit before sending,
Then it is recorded as edited, with both versions stored.

Given I dismiss it,
Then it is recorded as rejected with an optional reason.

Given the draft would contain an unresolved placeholder,
Then it is shown as a visible marker exactly as quick replies do.

Given the model returns content below the configured confidence floor,
Then no suggestion is shown, because a bad suggestion is worse than none.

Given generation fails,
Then the composer works normally and the failure is logged.

Given the accept, edit and reject rates,
Then they are visible in the AI usage report so the feature can be judged on evidence.
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

- **Blocked by / related ids:** CS-701-ticket-summaries
- **Depends on code areas or other stories:**

- CS-701 for the AI service, suggestion model and review flow.
- CS-404 for the placeholder resolver this reuses.
- CS-604 for the knowledge base search used as grounding.
- `TicketMessage.AiSuggestionId` for attribution.

## Extra notes (optional)

- Grounding replies in retrieved knowledge base content is what keeps them accurate. An ungrounded model will invent policies.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Retrieve articles first, then pass them as context. Cite them back to the agent so a wrong retrieval is visible.

## Out of scope

- What this story explicitly does **not** cover:

- Sending a reply without agent review. This is deliberately excluded.
- Automatic tone adjustment per customer.
