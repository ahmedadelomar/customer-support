# Story 28 — Mentions, watchers, presence and handover (Story: CS-405-team-collaboration)

## Prerequisites

- CS-201 (internal notes), CS-203 (reassignment) and CS-303 (SignalR hub) must be complete.
- **Reuse the CS-303 hub for presence.** A second real-time transport doubles the operational surface for no benefit.

## Story Goal

Agents pull in colleagues without the customer seeing anything. Mentions notify and create watchers,
presence shows who else is on the ticket, and a collision warning stops two agents replying at once.

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/CS-405-team-collaboration/intake.md`.
2. [backend/src/CustomerSupport.Domain/Workspace/TicketMention.cs](backend/src/CustomerSupport.Domain/Workspace/TicketMention.cs) — the unread index is `(MentionedUserId, ReadAt, MentionedAt)`.
3. [backend/src/CustomerSupport.Domain/Tickets/TicketWatcher.cs](backend/src/CustomerSupport.Domain/Tickets/TicketWatcher.cs) — `AddedByAutomation` distinguishes rule-added watchers.
4. [backend/src/CustomerSupport.Api/Hubs/ChatHub.cs](backend/src/CustomerSupport.Api/Hubs/ChatHub.cs) (CS-303) — the hub pattern to extend.
5. [backend/src/CustomerSupport.Application/Common/Interfaces/QueryableExtensions.cs](backend/src/CustomerSupport.Application/Common/Interfaces/QueryableExtensions.cs) — `WhereTicketVisible`, used to check whether a mentioned colleague can actually see the ticket.

## Product rules (from story)

- **Mentions are parsed and stored as `TicketMention` rows**, not left as text to be re-parsed at render time.
- **A mention adds the mentioned user as a watcher**, so they follow what happens next without a second action.
- **Mentioning someone who cannot see the ticket warns the author** before posting, naming the reason.
- **Mentions and internal notes never appear in any customer-facing view.**
- **Presence is ephemeral** — hub state only, never persisted.
- **The collision warning is advisory.** Never block a send; two agents replying is sometimes deliberate.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/mentions` | `workspace.collaborate` | The caller's mentions, unread first. |
| POST | `/api/mentions/{id}/read` | owner | |
| POST | `/api/tickets/{id}/watch` , `/unwatch` | `tickets.view` | |
| GET | `/api/tickets/{id}/watchers` | `tickets.view` | |
| GET | `/api/users/mentionable` | `workspace.collaborate` | Colleagues who can see the given ticket. |
| WS | `/hubs/collaboration` | token | Presence and composing signals. |

## Backend Tasks

### 1 — Mention parsing and visibility warning

**File:** `backend/src/CustomerSupport.Application/Workspace/Collaboration/`

Notes carry mentions as structured markers (`@[Display Name](userId)`) produced by the editor, so parsing is deterministic rather than guessing at names:

```csharp
private static readonly Regex MentionPattern =
    new(@"@\[([^\]]+)\]\(([0-9a-fA-F-]{36})\)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
```

For each mentioned id: create a `TicketMention`, add a `TicketWatcher` if absent, and dispatch a notification.

`GET /api/users/mentionable?ticketId=` returns colleagues who can actually see the ticket, by running the candidate set through the same `WhereTicketVisible` rule. Users outside it are still returned but flagged `canView: false`, so the editor can warn rather than silently excluding them — the author may still have a good reason.

### 2 — Collaboration hub

**File:** `backend/src/CustomerSupport.Api/Hubs/CollaborationHub.cs`

Groups per ticket (`ticket:{id}`). Methods: `JoinTicket`, `LeaveTicket`, `StartComposing`, `StopComposing`.

Authorise `JoinTicket` against ticket visibility, exactly as the chat hub authorises per session — otherwise presence leaks who is working on tickets a user cannot see.

Broadcast presence as `{ userId, displayName, isComposing }`. Keep state in memory only; on disconnect, remove and broadcast. Expire composing state after 30 seconds without a keystroke so a closed laptop does not show as permanently composing.

## Frontend Tasks

### 3 — Mention editor

In the internal-note composer, typing `@` opens a colleague picker filtered as the author types. Selecting inserts the structured marker and renders it as a chip.

When a selected colleague has `canView: false`, show an inline warning: "Sara cannot see this ticket (different department). They will be notified but unable to open it."

Render mentions in posted notes as chips linking to the colleague, and never render them in any portal view.

### 4 — Collaboration inbox and watchers

A `/agent/mentions` page and a badge in the top bar showing the unread count, with rows linking to the ticket and marking read on open.

A watchers list in the ticket properties sidebar with avatars, a **Watch** / **Unwatch** toggle, and a marker on watchers added by automation so it is clear why they are there.

### 5 — Presence and collision warning

Join the ticket hub group on opening a ticket, leave on close. Show viewer avatars in the ticket header.

When another agent is composing while the local agent has text in the composer, show an amber inline warning: "Ahmed is also replying to this ticket." Advisory only — never disable send. Clear it when they stop or send.

## Verification Steps

1. Mention a colleague in an internal note: they are notified, added as a watcher, and it appears in their inbox.
2. Mention someone from another department: the warning appears before posting, and posting still works.
3. Open the mentions inbox: unread first, and opening one marks it read.
4. Watch a ticket you are not assigned: you receive activity notifications; unwatch stops them.
5. Open the same ticket as two agents: each sees the other in the presence indicator.
6. Start typing as both: each sees the collision warning, and neither send button is disabled.
7. Close one browser: the presence indicator clears for the other within a few seconds.
8. Leave a composer idle for 30 seconds: the composing state expires.
9. Try to join the hub group for a ticket you cannot see: refused.
10. Confirm mentions and internal notes are absent from every portal endpoint and view.
11. Reassign with a handover note: it is recorded as an internal note and shown to the new assignee.

## Done Criteria

- [ ] Mentions are stored structurally, notify, and create watchers.
- [ ] Mentioning a colleague without visibility warns but does not block.
- [ ] The collaboration inbox and unread badge work.
- [ ] Watchers can be added and removed, with automation-added ones marked.
- [ ] Presence and collision warnings work over the existing hub, authorised per ticket.
- [ ] Presence is never persisted, and composing state expires.
- [ ] Nothing collaborative leaks into a customer-facing view.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
