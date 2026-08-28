# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/communication-channels/CS-301-email-channel/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 3 — Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-301-email-channel`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `channels, email`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Email
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to email support and get replies in the same thread,
So that I can use the channel I already use for everything else.

Scope:
- Inbound email polling or webhook ingestion into tickets.
- Threading: a reply to an existing ticket joins that ticket rather than opening a new one.
- Outbound replies sent from the support mailbox with the ticket reference in the subject.
- Attachment extraction from inbound mail.
- Auto-acknowledgement on ticket creation, in the customer's language.
- Bounce and delivery-failure handling.
- Loop protection against auto-responders.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given an email arrives at a configured support mailbox,
Then a ticket is created for the matching customer,
And the sender address is matched against normalised contact values,
And an unknown sender creates a new customer profile automatically.

Given the same message is delivered twice by the provider,
Then only one ticket and one message are created, because ingestion is idempotent on the provider message id.

Given a reply arrives referencing an existing ticket, by In-Reply-To, References, or the ticket number in the subject,
Then it is appended to that ticket instead of creating a new one,
And the ticket reopens if it was resolved.

Given an inbound email with attachments,
Then each attachment is stored and linked to the message,
And oversized or disallowed attachments are skipped with a note on the message rather than failing the whole ingestion.

Given an agent replies,
Then the message is sent from the mailbox with the ticket number in the subject,
And a delivery log entry is created,
And the branded header and footer are applied.

Given an inbound message carries auto-submitted or precedence bulk headers,
Then no auto-acknowledgement is sent, to avoid a mail loop.

Given a send fails or bounces,
Then the delivery log records the failure,
And the assigned agent is notified so a human can react.

Given the mailbox is unreachable,
Then the failure is recorded on the channel account and surfaced in the admin UI rather than failing silently.
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

- CS-201 for tickets and messages, CS-102 for contact matching, CS-104 for attachments.
- `Channel`, `ChannelAccount`, `MessageDeliveryLog` entities — already defined.
- `TicketMessage.ExternalMessageId` with its unique filtered index — the idempotency guarantee.
- CS-1102 style integration connection for provider credentials.

## Extra notes (optional)

- Email is the highest-volume channel in most support operations, and the one where duplicate ingestion causes the most visible damage.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Prefer provider webhooks over IMAP polling where available; keep the polling path as a fallback since not every mailbox supports webhooks.

## Out of scope

- What this story explicitly does **not** cover:

- A full mail server. The system connects to an existing provider.
- Email marketing or campaigns.
