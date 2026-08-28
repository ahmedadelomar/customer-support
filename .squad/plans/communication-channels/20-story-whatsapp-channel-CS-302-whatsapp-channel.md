# Story 20 — WhatsApp Business messaging (Story: CS-302-whatsapp-channel)

## Prerequisites

- CS-301 must be complete — this story is an adapter onto its pipeline, not a parallel implementation.
- A WhatsApp Business provider account with a verified number and approved templates is required before end-to-end testing.

## Story Goal

Customers reach support on WhatsApp. Inbound messages flow through the shared pipeline; outbound
respects the platform's 24-hour service window, falling back to approved templates with an explanation the
agent can act on rather than a silent failure.

## Context — Read These Files First

1. `.squad/stories/communication-channels/CS-302-whatsapp-channel/intake.md`.
2. [backend/src/CustomerSupport.Application/Channels/Inbound/IInboundMessagePipeline.cs](backend/src/CustomerSupport.Application/Channels/Inbound/IInboundMessagePipeline.cs) (CS-301) — the interface to adapt onto.
3. [backend/src/CustomerSupport.Domain/Channels/ChannelAccount.cs](backend/src/CustomerSupport.Domain/Channels/ChannelAccount.cs) — `SettingsJson` holds the phone number id and template namespace.
4. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — `LastCustomerReplyAt` is what the 24-hour window is measured from.
5. [backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs](backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs) — phone normalisation must match how contacts were stored.

## Product rules (from story)

- **The 24-hour service window is measured from `LastCustomerReplyAt`.** Outside it, free-form sending is blocked by the platform, so the product must block it first and explain why.
- **Webhook signatures are verified before any processing.** An unverified webhook endpoint lets anyone inject messages into customer tickets.
- **Media is downloaded from the provider and stored locally.** Provider media URLs expire, so linking to them loses the attachment.
- **Delivery and read receipts update the message indicator**, because "did they see it" is the question agents ask most on this channel.
- **Phone matching uses the shared normaliser.** WhatsApp delivers E.164 without a plus; normalise before matching.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Webhook with signature verification

**File:** `backend/src/CustomerSupport.Api/Controllers/Webhooks/WhatsAppWebhookController.cs`

Verify the provider signature over the **raw** request body before deserialising — deserialising first means processing unverified input:

```csharp
[HttpPost("{accountId:guid}")]
[AllowAnonymous]
public async Task<IActionResult> Receive(Guid accountId, CancellationToken ct)
{
    Request.EnableBuffering();
    using var reader = new StreamReader(Request.Body, leaveOpen: true);
    var raw = await reader.ReadToEndAsync(ct);
    Request.Body.Position = 0;

    if (!await verifier.IsValidAsync(accountId, raw, Request.Headers["X-Hub-Signature-256"]!, ct))
    {
        return Unauthorized();
    }
    ...
}
```

Compare with a fixed-time comparison (`CryptographicOperations.FixedTimeEquals`) to avoid leaking the signature through timing.

Return 200 quickly and process asynchronously: providers retry aggressively on slow responses, which turns one message into several redeliveries.

Also implement the GET verification challenge the provider uses when the webhook is first registered.

### 2 — Adapter and media download

Map the provider payload to `InboundMessage`, using the WhatsApp message id (`wamid...`) as `ExternalMessageId` so the pipeline's idempotency applies unchanged.

For media messages, resolve the media id to a temporary URL, download within the same operation, and store through `IFileStorage`. Provider URLs typically expire within minutes — a background job scheduled for later will find them gone.

Map the message type: text, image, document, audio, video, location and contacts. Unsupported types should still create a message noting what was received, rather than being dropped.

### 3 — Service-window enforcement and templates

**File:** `backend/src/CustomerSupport.Application/Channels/WhatsApp/`

```csharp
public record SendWindowState(bool IsOpen, DateTimeOffset? ClosesAt, TimeSpan? Remaining);

public SendWindowState EvaluateWindow(Ticket ticket)
{
    if (ticket.LastCustomerReplyAt is not { } last) return new(false, null, null);
    var closes = last.AddHours(24);
    var now = clock.UtcNow;
    return now < closes ? new(true, closes, closes - now) : new(false, closes, null);
}
```

Expose it on the ticket detail response so the UI can show the countdown. When the window is closed, refuse a free-form send with a 409 whose body lists the approved templates, so the client can offer them without a second request.

Add `GET /api/channels/whatsapp/templates` returning approved templates with their parameter definitions, and a send-template command that validates parameter counts before calling the provider.

## Frontend Tasks

### 4 — Service-window indicator in the composer

On a WhatsApp ticket, show the window state above the composer:

- Open: a subtle countdown, "Free-form replies available for 6h 12m".
- Closing within an hour: amber.
- Closed: the free-form composer is disabled with an explanation, and a **Send template** button replaces it.

This is the difference between an agent understanding the platform rule and an agent seeing a mysterious send failure.

### 5 — Template picker and media rendering

A dialog listing approved templates with a live preview as parameters are filled, validating that every placeholder has a value before enabling send.

Render inbound media in the thread: images inline with a lightbox, documents as download chips with filename and size, audio with a player, location as coordinates with a map link.

Show delivery state per outbound message using the WhatsApp convention agents recognise: one tick sent, two ticks delivered, two blue ticks read.

## Verification Steps

1. Register the webhook and confirm the GET verification challenge succeeds.
2. Send a WhatsApp message: a ticket is created and the customer matched by normalised number.
3. Replay the webhook: no duplicate message.
4. Post to the webhook with an invalid signature: 401, and nothing is ingested.
5. Send an image: it is downloaded, stored, and renders inline in the thread.
6. Reply within 24 hours: the free-form message is delivered.
7. Set `LastCustomerReplyAt` to 25 hours ago: the composer disables, explains why, and offers templates.
8. Send a template with a missing parameter: rejected before the provider call.
9. Confirm delivered and read receipts update the message indicator.
10. Confirm a customer whose number was stored as `+966 50 123 4567` still matches when WhatsApp delivers `966501234567`.

## Done Criteria

- [ ] The WhatsApp adapter reuses `IInboundMessagePipeline` with no duplicated matching or threading logic.
- [ ] Webhook signatures are verified over the raw body with a fixed-time comparison, before deserialisation.
- [ ] Media is downloaded and stored locally, not linked to expiring provider URLs.
- [ ] The 24-hour window is enforced server-side and explained in the UI with a countdown.
- [ ] Templates are listed, validated and sendable.
- [ ] Delivery and read receipts are reflected per message.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
