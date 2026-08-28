# Story 21 — Live chat widget, queue and agent console (Story: CS-303-live-chat)

## Prerequisites

- CS-201 (tickets), CS-1203 (teams for routing) and CS-103 (interaction timeline) must be complete.
- `Program.cs` already reads the JWT from the query string for `/hubs` paths. Do not add a second auth mechanism for the hub.

## Story Goal

Real-time chat from the public site and the portal. Visitors queue, agents handle several conversations
at once, chats can become tickets, and every transcript lands on the customer timeline.

## Context — Read These Files First

1. `.squad/stories/communication-channels/CS-303-live-chat/intake.md`.
2. [backend/src/CustomerSupport.Domain/Channels/ChatSession.cs](backend/src/CustomerSupport.Domain/Channels/ChatSession.cs) — note `VisitorKey` (anonymous before identification), `Status`, `IsBotHandled` and `QueuedForTeamId`.
3. [backend/src/CustomerSupport.Domain/Channels/ChatMessage.cs](backend/src/CustomerSupport.Domain/Channels/ChatMessage.cs) — the class remark explains why chat messages are separate from ticket messages.
4. [backend/src/CustomerSupport.Api/Program.cs](backend/src/CustomerSupport.Api/Program.cs) — the `OnMessageReceived` handler for `/hubs`.
5. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — the `(Status, QueuedForTeamId, StartedAt)` index the console polls on.
6. CS-705, which will front this channel with the AI chatbot; leave `IsBotHandled` and `HandedOverAt` wired even though nothing sets them yet.

## Product rules (from story)

- **A chat starts anonymous.** `VisitorKey` is browser-scoped; the session links to a customer only on identification or portal sign-in.
- **A chat is not a ticket.** It becomes one only when promoted, which copies the transcript.
- **Every ended chat writes an `Interaction`**, whether or not it became a ticket.
- **Abandoned sessions are timed out**, not left open forever — the queue must reflect reality.
- **Agents have a concurrent-chat limit** separate from their ticket capacity; chats demand continuous attention.
- **Visitor input is untrusted.** Render as text, never HTML.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/api/chat/sessions` | anonymous | Starts a session; returns the visitor key and a scoped token. |
| GET | `/api/chat/sessions/{id}/messages` | session token or `channels.livechat.handle` | Transcript. |
| POST | `/api/chat/sessions/{id}/identify` | session token | Links to a customer. |
| POST | `/api/chat/sessions/{id}/end` | either party | |
| POST | `/api/chat/sessions/{id}/rate` | session token | Post-chat rating. |
| GET | `/api/chat/queue` | `channels.livechat.handle` | Waiting and active sessions for the agent's teams. |
| POST | `/api/chat/sessions/{id}/accept` | `channels.livechat.handle` | Race-safe claim. |
| POST | `/api/chat/sessions/{id}/promote` | `tickets.create` | Creates a ticket with the transcript. |
| WS | `/hubs/chat` | token | SignalR hub. |

## Backend Tasks

### 1 — SignalR chat hub

**File:** `backend/src/CustomerSupport.Api/Hubs/ChatHub.cs`

Groups per session (`chat:{sessionId}`) and per team (`chatqueue:{teamId}`), so a new waiting session notifies only the right agents.

Methods: `SendMessage`, `Typing`, `MarkRead`, `JoinSession`, `LeaveSession`.

**Authorise every method against the session**, not just the connection. A visitor token grants access to exactly one session; an agent token grants access to sessions in their teams. Without this check, any connected visitor could join any session by id.

Persist each message before broadcasting, so a reconnecting client fetching the transcript sees the same history as the live stream.

### 2 — Queue, routing and race-safe accept

A new session is queued to the team resolved from the widget's channel account. `GET /api/chat/queue` returns waiting sessions ordered by `StartedAt` with a waiting-time counter, plus the agent's active sessions.

Accept uses the same conditional-update pattern as ticket claiming (CS-203):

```csharp
var updated = await db.ChatSessions
    .Where(s => s.Id == id && s.AssignedAgentId == null && s.Status == "Waiting")
    .ExecuteUpdateAsync(s => s
        .SetProperty(x => x.AssignedAgentId, currentUser.UserId)
        .SetProperty(x => x.Status, "Active")
        .SetProperty(x => x.FirstAgentReplyAt, (DateTimeOffset?)null), ct);

if (updated == 0) throw new ConflictException("This chat has already been accepted.");
```

Enforce the concurrent-chat limit before accepting.

If no agent accepts within the configured wait, offer the offline path: capture a message and create a ticket.

### 3 — Promotion, ending and abandonment

`PromoteChatToTicketCommand` creates a ticket on channel `LiveChat`, renders the transcript into the description (or the first message), links `ChatSession.TicketId`, and records the promotion in both places.

Ending a session sets `EndedAt`, computes `MessageCount`, writes an `Interaction` with a transcript preview, and offers the rating.

`AbandonStaleChatSessionsJob` (Quartz, every 5 minutes) marks sessions with no activity for the configured timeout as `Abandoned` and writes the interaction, so the queue never fills with ghosts.

## Frontend Tasks

### 4 — Embeddable widget

**File:** `frontend/src/app/features/portal/chat-widget/`

A self-contained bundle loadable by a one-line script tag, rendering a launcher and a panel. It must not inherit host-page styles — scope everything, or use a shadow root.

Persist `visitorKey` in `localStorage` in a try/catch (private browsing blocks it; the widget must still work for that session). Reconnect with backoff and replay missed messages by fetching the transcript on reconnect.

Show queue position or waiting time so a waiting visitor knows something is happening.

### 5 — Agent chat console

**File:** `frontend/src/app/features/agent/chat/chat-console.page.ts`

A two-pane layout: waiting and active sessions on the left with unread badges and waiting times, the selected conversation on the right with the customer panel beside it.

Handle several chats at once: switching sessions must not lose an unsent draft, so keep drafts in a signal keyed by session id.

Actions: **Accept**, **Promote to ticket**, **End chat**, **Transfer to another agent**.

Play a sound and show a browser notification for a new queued chat, both user-toggleable — an agent who misses the queue defeats the channel.

## Verification Steps

1. Open the widget on a test page: a session starts and appears in the agent queue in real time.
2. Accept from one agent while another accepts simultaneously: one succeeds, the other gets a clear conflict message.
3. Exchange messages: both sides update in real time, and typing indicators and read receipts work.
4. Refresh the visitor browser mid-chat: the session resumes with full history.
5. Kill the WebSocket and restore it: the client reconnects and replays missed messages.
6. Try to join another session by changing the id in the client: refused.
7. Send `<img src=x onerror=alert(1)>` as a visitor: it renders as text in the agent console.
8. Promote a chat: a ticket is created with the transcript and the session links to it.
9. End a chat: the transcript appears on the customer timeline and the rating prompt shows.
10. Leave a session idle past the timeout: it is marked abandoned and leaves the queue.
11. With no agent available, confirm the offline path captures a message and creates a ticket.
12. Handle three chats at once and confirm drafts survive switching between them.

## Done Criteria

- [ ] SignalR hub with per-session authorisation on every method, not just on connect.
- [ ] Queue, race-safe accept, concurrent-chat limits and the offline fallback all work.
- [ ] The widget is self-contained, style-isolated, and reconnects with history.
- [ ] Promotion copies the transcript and links both records.
- [ ] Ended and abandoned sessions write interactions.
- [ ] Visitor content is rendered as text.
- [ ] The console handles concurrent chats with per-session drafts and notifications.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
