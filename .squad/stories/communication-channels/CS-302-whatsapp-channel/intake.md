# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/communication-channels/CS-302-whatsapp-channel/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 3 — Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-302-whatsapp-channel`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `channels, whatsapp`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
WhatsApp
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to reach support on WhatsApp,
So that I can use the messaging app I already have open.

Scope:
- Inbound WhatsApp messages via provider webhook, creating or continuing tickets.
- Outbound replies within the 24-hour service window.
- Template messages for replies outside that window, since free-form messages are not permitted then.
- Media handling: images, documents, audio.
- Delivery and read receipts reflected on the message.
- Phone-number matching against customer contacts.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a WhatsApp message arrives,
Then the sender number is normalised and matched against customer contacts,
And a ticket is created or the open one continued.

Given the provider redelivers a webhook,
Then the message is not duplicated, because ingestion is idempotent on the WhatsApp message id.

Given an agent replies within 24 hours of the customer's last message,
Then a free-form message is sent.

Given an agent replies more than 24 hours after the customer's last message,
Then free-form sending is blocked,
And the agent is offered approved templates instead,
And the UI explains why.

Given the customer sends media,
Then it is downloaded from the provider and stored as an attachment on the message.

Given the provider reports delivered or read,
Then the delivery log and the message indicator update.

Given the webhook signature does not verify,
Then the request is rejected, because an unverified webhook is an open door for message injection.

Given outbound sending fails,
Then the failure and provider error code are recorded and the agent is notified.
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

- CS-301 for the shared ingestion pipeline this story plugs into.
- `ChannelAccount.SettingsJson` for the template namespace and phone number id.
- CS-1103 for provider credentials.

## Extra notes (optional)

- The 24-hour service window is a WhatsApp platform rule, not a product choice. The UI must make it visible rather than letting sends fail mysteriously.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Reuse the same normalised phone value contacts are stored with, or matching will fail on formatting differences.

## Out of scope

- What this story explicitly does **not** cover:

- WhatsApp template authoring and approval, which happens in the provider console.
- Broadcast messaging.
