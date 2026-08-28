# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/agent-dashboard/CS-404-quick-replies/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 4 — Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-404-quick-replies`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `workspace, productivity`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Quick replies
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want reusable canned responses with placeholders,
So that I answer common questions consistently and quickly, in the right language.

Scope:
- Bilingual reply templates with a title and body.
- Placeholder tokens resolved from the ticket and customer at insertion.
- Personal, team and global scopes.
- Keyboard shortcuts that expand in the composer.
- Search and category filtering in the picker.
- Usage counting so the most-used surface first.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I open the quick reply picker in the composer,
Then I see replies available to me across personal, team and global scopes,
Ordered by usage,
And I can search and filter by category.

Given I insert a quick reply,
Then the body for the ticket's language is inserted,
And placeholder tokens are replaced with real values.

Given a placeholder cannot be resolved,
Then it is replaced with a clearly visible marker rather than being left as a raw token or silently blanked,
So the agent notices before sending.

Given I type a shortcut such as /refund followed by a space,
Then the reply expands inline in the composer.

Given a shortcut that is ambiguous within my scope,
Then saving it is refused.

Given I create a quick reply,
Then I set both language bodies, and can restrict it to a category or channel.

Given a global quick reply,
Then creating or editing it requires the manage-global permission.

Given I insert a reply,
Then its usage count increases,
And the message records which quick reply it came from.

Given the ticket language is Arabic,
Then the Arabic body is inserted even though my interface is in English.
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

- `QuickReply` entity — already defined, including the shortcut uniqueness index and `UsageCount`.
- `TicketMessage.QuickReplyId` for attribution.
- CS-201 for the composer.

## Extra notes (optional)

- Inserting the reply in the ticket language, not the agent interface language, is the detail that makes this useful in a bilingual support team.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Placeholder resolution runs server-side on insert preview, so the token vocabulary lives in one place.

## Out of scope

- What this story explicitly does **not** cover:

- AI-suggested replies — CS-702.
- Rich media in templates.
