# Story 19 — Email ingestion, threading and outbound replies (Story: CS-301-email-channel)

## Prerequisites

- CS-201 (tickets), CS-102 (contacts and the shared normaliser) and CS-104 (attachments) must be complete.
- **This story owns the shared inbound pipeline.** Design `IInboundMessagePipeline` for WhatsApp, SMS and web forms too — they are the next three stories and must not each build their own.

## Story Goal

Email works end to end: messages arrive, land on the right ticket for the right customer, attachments
are extracted, agents reply from the support mailbox, and delivery is tracked. Duplicate deliveries never
create duplicate tickets.

## Context — Read These Files First

1. `.squad/stories/communication-channels/CS-301-email-channel/intake.md`.
2. [backend/src/CustomerSupport.Domain/Channels/ChannelAccount.cs](backend/src/CustomerSupport.Domain/Channels/ChannelAccount.cs) — one row per mailbox, with defaults, signature, auto-reply and `SettingsJson`.
3. [backend/src/CustomerSupport.Domain/Channels/MessageDeliveryLog.cs](backend/src/CustomerSupport.Domain/Channels/MessageDeliveryLog.cs) — per-attempt record updated by provider webhooks.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs) — `TicketMessageConfiguration` declares the unique filtered index on `ExternalMessageId`. **This is the idempotency guarantee.**
5. [backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs](backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs) (CS-102) — the only permitted normalisation.
6. [backend/src/CustomerSupport.Domain/Integrations/OutboxMessage.cs](backend/src/CustomerSupport.Domain/Integrations/OutboxMessage.cs) — outbound sends go through the outbox so a provider outage does not lose replies.

## Product rules (from story)

- **Ingestion is idempotent on the provider message id.** Insert and catch the unique-index violation; do not check-then-insert, which races.
- **Threading order:** `In-Reply-To` / `References` against a stored message id, then the ticket number parsed from the subject, then a new ticket. Never thread on subject text alone — "Re: Question" would merge unrelated tickets.
- **An unknown sender creates a customer**, so no message is ever dropped for lack of a profile.
- **A reply to a resolved ticket reopens it** (CS-204); a reply to a closed ticket creates a linked follow-up.
- **Oversized or disallowed attachments are skipped with a note on the message**, never a failed ingestion — losing the message body because of one bad attachment is worse.
- **No auto-acknowledgement to `Auto-Submitted: auto-*` or `Precedence: bulk` senders.** This is mail-loop protection, and without it two auto-responders will message each other indefinitely.
- **Outbound goes through the outbox**, so a provider outage delays rather than loses replies.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/api/webhooks/email/{accountId}` | signature-verified, anonymous | Provider inbound webhook. |
| POST | `/api/webhooks/email/{accountId}/status` | signature-verified, anonymous | Delivery, bounce and complaint callbacks. |
| GET/POST/PUT | `/api/channel-accounts` | `channels.manage` | Mailbox configuration. |
| POST | `/api/channel-accounts/{id}/test` | `channels.manage` | Connectivity check; updates `LastPollError`. |

## Backend Tasks

### 1 — The shared inbound pipeline

**File:** `backend/src/CustomerSupport.Application/Channels/Inbound/IInboundMessagePipeline.cs`

Every inbound channel funnels through one implementation. Get this interface right and stories 20, 22 and 23 become thin adapters.

```csharp
public record InboundMessage(
    ChannelKey Channel,
    Guid ChannelAccountId,
    string ExternalMessageId,
    string? InReplyToExternalId,
    string FromAddress,        // email, or E.164 phone
    string? FromDisplayName,
    string? Subject,
    string BodyText,
    string? BodyHtml,
    DateTimeOffset SentAt,
    IReadOnlyList<InboundAttachment> Attachments,
    IReadOnlyDictionary<string, string> Headers);

public interface IInboundMessagePipeline
{
    /// <summary>Idempotent. Returns the ticket the message landed on, or null if it was a duplicate.</summary>
    Task<Guid?> IngestAsync(InboundMessage message, CancellationToken ct = default);
}
```

The implementation, in order:

1. **Duplicate check by insert.** Attempt the whole operation; on a unique-index violation for `ExternalMessageId`, swallow it and return null. Checking first and inserting after races with a concurrent redelivery.
2. **Resolve the customer** — normalise `FromAddress` with `ContactNormalizer`, look up `CustomerContact.NormalizedValue`; create a customer and contact if unmatched.
3. **Resolve the ticket** — `InReplyToExternalId` against stored message ids, then the ticket number regex on the subject, then create.
4. **Reopen or follow up** per CS-204 rules.
5. **Persist the message, attachments, ticket event and interaction in one transaction.**
6. **Queue the auto-acknowledgement** unless loop headers are present.

### 2 — Email adapter, threading and loop protection

**File:** `backend/src/CustomerSupport.Infrastructure/Channels/Email/`

An `EmailIngestionService` that maps a provider payload (or a polled IMAP message) to `InboundMessage`.

Threading:

```csharp
// 1. RFC 5322 headers — the reliable signal.
var references = headers.GetValueOrDefault("References")?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
var candidates = new[] { headers.GetValueOrDefault("In-Reply-To") }
    .Concat(references ?? []).Where(x => !string.IsNullOrWhiteSpace(x));

var ticketId = await db.TicketMessages
    .Where(m => candidates.Contains(m.ExternalMessageId))
    .Select(m => (Guid?)m.TicketId)
    .FirstOrDefaultAsync(ct);

// 2. Fall back to the ticket number in the subject.
ticketId ??= await MatchByTicketNumber(subject, ct);   // /\bTCK-\d{4}-\d{6}\b/
```

Loop protection, checked before queueing the auto-reply:

```csharp
static bool IsAutomated(IReadOnlyDictionary<string, string> h) =>
    h.TryGetValue("Auto-Submitted", out var auto) && !auto.Equals("no", StringComparison.OrdinalIgnoreCase)
    || h.TryGetValue("Precedence", out var p) && p is "bulk" or "list" or "junk"
    || h.ContainsKey("List-Id")
    || h.ContainsKey("X-Auto-Response-Suppress");
```

### 3 — Outbound send via the outbox

**File:** `backend/src/CustomerSupport.Infrastructure/Channels/Email/EmailSender.cs`

Replying writes an `OutboxMessage` in the same transaction as the `TicketMessage`, and a dispatcher sends it. This is why the outbox entity exists: without it, a provider outage during a reply loses the reply.

The sender sets `Subject = "[{ticket.Number}] {subject}"`, generates and stores a `Message-ID` as `ExternalMessageId` so the customer's reply threads back, appends the account signature and the branded wrapper (CS-1205) in the customer's language, and writes a `MessageDeliveryLog` row.

The status webhook updates that row by `ProviderMessageId` and, on a hard bounce, marks the contact unverified and notifies the assigned agent — a silently bouncing address is how tickets go unanswered.

### 4 — Mailbox administration and health

CRUD for `ChannelAccount` behind `channels.manage`. Store credentials by reference to an `IntegrationConnection`, never inline.

Add a **Test connection** action that attempts a connect and writes `LastPolledAt` / `LastPollError`. Surface a red banner on the channels screen when an account has an error, so a broken mailbox is visible rather than discovered days later through missing tickets.

For providers without webhooks, add a Quartz polling job per account, with per-account concurrency locking so a slow poll cannot overlap itself.

## Frontend Tasks

### 5 — Channel accounts admin

**File:** `frontend/src/app/features/admin/channels/`

List of channel accounts with the channel icon, identifier, default department, active state and a health indicator driven by `lastPollError`. The form covers identifier, integration connection, defaults, bilingual signature, auto-reply toggle and body, and channel-specific `settingsJson` fields rendered per channel type.

Include the **Test connection** button with an inline result.

### 6 — Email rendering in the ticket thread

Inbound email bodies are untrusted HTML. Render them sanitised — strip `<script>`, event handlers, `<iframe>`, `<object>` and external CSS — inside a container that constrains images to the thread width. Offer a "view original" toggle showing the plain-text alternative.

Show delivery state per outbound message as a small indicator (queued, sent, delivered, failed) sourced from the delivery log, with the provider error on hover when failed.

## Verification Steps

1. Send an email to the configured mailbox: a ticket is created, the customer matched, and the body appears as the first message.
2. Replay the same provider webhook payload: no second ticket and no second message.
3. Reply from the customer mail client: the message threads onto the same ticket via `In-Reply-To`.
4. Send a fresh email quoting the ticket number in the subject: it threads onto that ticket.
5. Send an email with subject "Re: Question" from a customer with two open tickets: it does **not** merge them, and creates a new ticket.
6. Send from an unknown address: a customer profile and contact are created.
7. Send with a 40MB attachment: the message is ingested and the attachment is skipped with a note.
8. Send with `Auto-Submitted: auto-replied`: the ticket is created but no auto-acknowledgement is sent.
9. Reply as an agent: the customer receives it with the ticket number in the subject and the branded footer, in their preferred language.
10. Stop the mail provider and reply: the outbox retains the message and it sends when the provider returns.
11. Trigger a hard bounce: the delivery log records it and the agent is notified.
12. Break the mailbox credentials: the admin screen shows the error banner.

## Done Criteria

- [x] `IInboundMessagePipeline` exists and is general enough for WhatsApp, SMS and web forms.
- [x] Ingestion is idempotent by unique-index violation, not by check-then-insert.
- [x] Threading follows headers, then ticket number, and never subject text alone.
- [x] Unknown senders create customers; oversized attachments are skipped without losing the message.
- [x] Loop protection prevents auto-acknowledging automated mail.
- [x] Outbound sends go through the outbox and are logged, with bounces surfaced to the agent.
- [x] Inbound HTML is sanitised before rendering.
- [x] Mailbox health is visible in the admin UI.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
