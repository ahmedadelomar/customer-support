# communication-channels — plan overview

Entry point for the **Section 3 — Communication Channels** feature. Stories execute in order by their `NN` prefix.

How messages get in and out. Story 19 builds the shared ingestion pipeline — customer matching,
idempotency, threading, attachment extraction — and stories 20, 22 and 23 plug into it rather than
reimplementing it. Story 21 (live chat) is the exception: it is real-time and pre-ticket, so it has its own
model.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 19 | [19-story-email-channel-CS-301-email-channel.md](19-story-email-channel-CS-301-email-channel.md) | Email ingestion, threading and outbound replies | CS-301-email-channel | 14 |
| 20 | [20-story-whatsapp-channel-CS-302-whatsapp-channel.md](20-story-whatsapp-channel-CS-302-whatsapp-channel.md) | WhatsApp Business messaging | CS-302-whatsapp-channel | 19 |
| 21 | [21-story-live-chat-CS-303-live-chat.md](21-story-live-chat-CS-303-live-chat.md) | Live chat widget, queue and agent console | CS-303-live-chat | 14 |
| 22 | [22-story-sms-channel-CS-304-sms-channel.md](22-story-sms-channel-CS-304-sms-channel.md) | SMS messaging with segment awareness | CS-304-sms-channel | 19 |
| 23 | [23-story-web-forms-CS-305-web-forms.md](23-story-web-forms-CS-305-web-forms.md) | Configurable public web forms | CS-305-web-forms | 19 |

## Dependency notes

- **Story 19 must land first.** It defines `IInboundMessagePipeline`, which WhatsApp, SMS and web forms all reuse. Building any of them first would produce four different customer-matching implementations.
- **Idempotency is the invariant of this whole feature.** Every provider redelivers webhooks. `TicketMessage.ExternalMessageId` has a unique filtered index for exactly this reason — rely on it rather than checking-then-inserting.
- All four inbound channels must match customers on `CustomerContact.NormalizedValue` using the shared `ContactNormalizer` from CS-102. A second normalisation rule silently misroutes messages.
- Every inbound and outbound message must also call `IInteractionRecorder` (CS-103), or the customer timeline will be missing the channel that matters most.
- Story 21 depends on SignalR. `Program.cs` already reads the token from the query string for `/hubs` paths, because the browser WebSocket API cannot send an Authorization header.
- Provider credentials live in `IntegrationConnection` (CS-1102), encrypted. No channel story should store credentials of its own.
