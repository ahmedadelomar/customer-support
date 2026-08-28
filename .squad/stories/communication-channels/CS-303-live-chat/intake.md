# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/communication-channels/CS-303-live-chat/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 3 — Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-303-live-chat`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `channels, realtime`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Live chat
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a website visitor,
I want to chat with support in real time,
So that I can get an answer without opening a ticket and waiting.

Scope:
- An embeddable chat widget for the public site and the customer portal.
- Real-time messaging over WebSocket, with typing indicators and read receipts.
- A queue with routing to an available agent in the right team.
- An agent chat console handling several conversations at once.
- Promotion of a chat to a ticket when follow-up is needed.
- Transcript retained on the customer timeline.
- Offline capture when no agent is available.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a visitor opens the widget,
Then a chat session starts with an anonymous visitor key,
And the session is queued to the configured team.

Given an agent is available,
Then the chat is routed to them and they are notified.

Given no agent is available,
Then the widget offers to take a message,
And a ticket is created from it.

Given an active chat,
Then messages appear in real time for both parties,
And typing indicators and read receipts work.

Given the visitor identifies themselves or is signed into the portal,
Then the session links to their customer profile,
And their history is visible to the agent.

Given an agent handles several chats,
Then the console shows each with an unread badge and the waiting time.

Given a chat needs follow-up,
When the agent promotes it to a ticket,
Then the transcript is copied to the ticket,
And the chat links to it.

Given a chat ends,
Then the transcript is written to the customer interaction timeline,
And the visitor is offered a rating.

Given a visitor closes the browser mid-chat,
Then the session is marked abandoned after a timeout rather than staying open forever.
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

- CS-201 for ticket promotion, CS-103 for the transcript timeline entry.
- `ChatSession` and `ChatMessage` entities — already defined.
- SignalR, and the JWT query-string handling already configured in `Program.cs` for `/hubs`.
- CS-1203 for team routing.

## Extra notes (optional)

- `Program.cs` already routes `/hubs` tokens from the query string, because the browser WebSocket API cannot set an Authorization header. That groundwork exists.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Chat messages are deliberately a separate entity from ticket messages: chats exist before, and often without, a ticket.

## Out of scope

- What this story explicitly does **not** cover:

- Video and voice calling.
- Co-browsing and screen sharing.
- The AI chatbot fronting chat — CS-705.
