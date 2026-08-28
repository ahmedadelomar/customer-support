# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/communication-channels/CS-304-sms-channel/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 3 — Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-304-sms-channel`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `channels, sms`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
SMS
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer without smartphone access,
I want to reach support by SMS and receive notifications,
So that I can use the channel that always works.

Scope:
- Inbound SMS ingestion into tickets via provider webhook.
- Outbound replies and notifications.
- Message segmentation awareness, including the much shorter limit for Arabic.
- Sender identity and opt-out handling.
- Delivery receipts.
- Cost visibility, since SMS is billed per segment.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given an SMS arrives,
Then the sender number is normalised and matched to a customer,
And a ticket is created or the open one continued.

Given an agent composes an SMS reply,
Then the UI shows the character count and the resulting segment count as they type,
And it accounts for Arabic text using UCS-2 encoding, where a segment is 70 characters rather than 160.

Given a message exceeds the configured maximum segments,
Then sending is blocked with a clear explanation rather than silently costing more.

Given a customer replies STOP or the Arabic equivalent,
Then their contact is marked as not allowing notifications,
And no further automated SMS is sent to it.

Given the provider reports delivery status,
Then the delivery log is updated.

Given sending fails,
Then the error code is recorded and the agent is notified.

Given SMS is used for a notification rather than a ticket reply,
Then it respects the recipient's notification preferences and quiet hours.
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

- **Blocked by / related ids:** CS-301-email-channel
- **Depends on code areas or other stories:**

- CS-301 for the shared ingestion pipeline.
- CS-102 for the notification opt-out flag that STOP handling sets.
- CS-504 for notification preferences and quiet hours.

## Extra notes (optional)

- Arabic SMS costs more than twice as much per character as Latin text. Showing the segment count as the agent types prevents surprise bills.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- GSM-7 allows 160 characters per segment, 153 when concatenated. UCS-2, which Arabic requires, allows 70 and 67.

## Out of scope

- What this story explicitly does **not** cover:

- Two-factor authentication codes by SMS.
- Bulk SMS campaigns.
