# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-management/CS-104-notes-attachments/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 1 — Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-104-notes-attachments`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `customers, files`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Notes and attachments
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a support agent,
I want to record internal notes and attach files to a customer,
So that context that does not belong in a ticket reply is still available to the team.

Scope:
- Free-text notes on a customer, optionally linked to the ticket being worked.
- Pinned notes surfaced at the top of the customer panel.
- Internal notes never exposed through the customer portal.
- File attachments on notes, customers, tickets and messages through one polymorphic attachment store.
- File type and size validation, and a virus scan hook.
- Secure download that authorises per request rather than relying on an unguessable URL.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a customer profile,
When I open the Notes tab,
Then I see notes newest first, with pinned notes above the rest.

Given I add a note,
Then the author and timestamp are recorded,
And I can attach one or more files in the same action.

Given a note is marked internal,
Then it is never returned by any customer portal endpoint.

Given I pin a note,
Then it appears at the top of the list and in the customer panel on the ticket screen.

Given I upload a file,
Then its extension is checked against the allowed list and its size against the configured maximum,
And an oversized or disallowed file is rejected with a clear message before any bytes are stored.

Given a file is uploaded,
Then it is stored under a generated storage key, never under its original filename,
And the original filename is preserved separately for display and download.

Given I request a download,
Then the request is authorised against the owning record before any bytes are served,
And the file is streamed rather than buffered.

Given a note I authored,
Then I can edit or delete it; notes by others are read-only unless I hold the manage permission.

Given a deleted note, it is soft-deleted and its attachments remain retrievable for audit.
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

- **Blocked by / related ids:** CS-101-customer-profiles
- **Depends on code areas or other stories:**

- CS-101 for the profile shell.
- `CustomerNote` and `Attachment` entities — already defined, with `Attachment` keyed by `(OwnerType, OwnerId)`.
- `IFileStorage` abstraction — declared, not yet implemented.
- CS-1004 for `attachments.maxBytes` and `attachments.allowedExtensions`.

## Extra notes (optional)

- The attachment store built here is reused by ticket messages (CS-201), KB articles (CS-602) and branding (CS-1205). Getting the authorisation model right once matters.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Never serve attachments from a static path. `OwnerType` and `OwnerId` exist precisely so the download endpoint can re-check permission against the owning record.

## Out of scope

- What this story explicitly does **not** cover:

- Inline image previews and thumbnail generation.
- Document versioning.
- Cloud storage providers beyond the local-disk implementation — the abstraction allows it, the adapter is a later story.
