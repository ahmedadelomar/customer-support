# Story 22 — SMS messaging with segment awareness (Story: CS-304-sms-channel)

## Prerequisites

- CS-301 must be complete — SMS is an adapter onto its pipeline.
- CS-102 must be complete for the contact opt-out flag that STOP handling sets.

## Story Goal

SMS in and out, with the segment and cost awareness this channel demands. Arabic messages cost more
than twice as much per character as Latin ones, so the composer shows the real segment count as the agent
types, and opt-out is honoured automatically.

## Context — Read These Files First

1. `.squad/stories/communication-channels/CS-304-sms-channel/intake.md`.
2. [backend/src/CustomerSupport.Application/Channels/Inbound/IInboundMessagePipeline.cs](backend/src/CustomerSupport.Application/Channels/Inbound/IInboundMessagePipeline.cs) (CS-301).
3. [backend/src/CustomerSupport.Domain/Customers/CustomerContact.cs](backend/src/CustomerSupport.Domain/Customers/CustomerContact.cs) — `AllowNotifications` is what STOP sets to false.
4. [backend/src/CustomerSupport.Domain/Channels/Channel.cs](backend/src/CustomerSupport.Domain/Channels/Channel.cs) — SMS is seeded with `SupportsAttachments = false`.
5. CS-504 for notification preferences and quiet hours, which outbound SMS notifications must respect.

## Product rules (from story)

- **Segment maths:** GSM-7 gives 160 characters per segment (153 concatenated); UCS-2, which Arabic requires, gives 70 (67 concatenated). A single Arabic character forces the whole message to UCS-2.
- **Block sends above the configured maximum segments** rather than letting them through and surprising finance.
- **STOP, UNSUBSCRIBE and the Arabic equivalents set `AllowNotifications = false`** on the matching contact and send one confirmation.
- **Opt-out blocks automated messages, not agent replies** to a conversation the customer started.
- **SMS carries no attachments.** Reference them by a link to the portal instead.
- **Outbound notifications respect quiet hours** (CS-504) unless the severity is critical.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Segment calculator

**File:** `backend/src/CustomerSupport.Application/Channels/Sms/SmsSegmentCalculator.cs`

Shared by the server (for validation) and mirrored on the client (for the live counter). Both must agree, or the agent sees one number and is charged for another.

```csharp
public record SmsSegments(int Length, int SegmentCount, string Encoding, int MaxLength);

public static SmsSegments Calculate(string text)
{
    // A single non-GSM character forces the entire message to UCS-2.
    var isGsm = text.All(c => GsmCharset.Contains(c));
    var perSegment = isGsm ? 160 : 70;
    var perConcatenated = isGsm ? 153 : 67;

    // GSM extended characters (^ { } [ ] ~ | € \) each consume two septets.
    var length = isGsm ? text.Sum(c => GsmExtended.Contains(c) ? 2 : 1) : text.Length;

    var segments = length <= perSegment
        ? 1
        : (int)Math.Ceiling(length / (double)perConcatenated);

    return new(length, segments, isGsm ? "GSM-7" : "UCS-2", perSegment);
}
```

Expose it at `POST /api/channels/sms/segments` so the client can verify without duplicating the charset tables, while still counting locally for responsiveness.

### 2 — Inbound adapter and opt-out handling

Map the provider payload to `InboundMessage` using the provider message id, and normalise the sender with `ContactNormalizer`.

Before the pipeline runs, intercept opt-out keywords:

```csharp
private static readonly string[] OptOut =
    ["STOP", "UNSUBSCRIBE", "CANCEL", "END", "QUIT", "إلغاء", "توقف", "الغاء"];

if (OptOut.Contains(body.Trim().ToUpperInvariant()))
{
    await optOutService.ApplyAsync(normalizedPhone, ct);   // sets AllowNotifications = false
    await sender.SendAsync(normalizedPhone, confirmationText, ct);
    return;   // no ticket for an opt-out keyword
}
```

Also handle START and the Arabic equivalent to opt back in.

### 3 — Outbound send with guards

Before sending, in order: check `AllowNotifications` on the target contact (skip for agent replies on a customer-initiated ticket), calculate segments and refuse above the cap, and for notifications check quiet hours.

Record segment count and estimated cost on the `MessageDeliveryLog` so the SMS cost report (Section 9) has real data rather than an estimate.

Send through the outbox like email, and update the delivery log from the provider status webhook.

## Frontend Tasks

### 4 — SMS composer with live segment counter

Below the composer show `{{length}}/{{maxLength}} · {{segments}} segment(s) · {{encoding}}`, updating as the agent types. Turn amber at 3 segments and red above the cap, with send disabled.

Warn explicitly when the encoding flips to UCS-2 — an agent adding one Arabic word to an English message will otherwise not understand why the segment count more than doubled.

Mirror `SmsSegmentCalculator` in TypeScript and add a shared test-vector fixture used by both the C# and TypeScript tests, so the two implementations cannot drift.

### 5 — Opt-out visibility

Show an opt-out badge on the contact in the customer panel and in the contact list (CS-102), with the date. When an SMS ticket's customer has opted out, show a banner on the composer explaining that automated messages are suppressed but a manual reply is still permitted.

## Verification Steps

1. Send an SMS to the configured number: a ticket is created and the customer matched.
2. Type a 200-character English message: the counter shows 2 segments, GSM-7.
3. Type a 100-character Arabic message: the counter shows 2 segments, UCS-2.
4. Add one Arabic character to a 150-character English message: the counter jumps and the encoding warning appears.
5. Confirm the client and server segment calculations agree across the shared test vectors.
6. Attempt to send above the segment cap: blocked with an explanation.
7. Reply STOP: the contact is opted out, one confirmation is sent, and no ticket is created.
8. Trigger an automated notification to that contact: suppressed.
9. Reply manually as an agent to that customer's ticket: still delivered.
10. Reply START: opted back in.
11. Confirm segment count and cost are recorded on the delivery log.
12. Send a notification during quiet hours: deferred unless critical.

## Done Criteria

- [x] Segment calculation is correct for GSM-7, GSM extended and UCS-2, and identical on client and server.
- [x] The composer shows length, segments and encoding live, and warns on the encoding flip.
- [x] Sends above the cap are blocked.
- [x] STOP and START in English and Arabic are handled, with one confirmation each.
- [x] Opt-out suppresses automated messages but never agent replies.
- [x] Quiet hours are respected for notifications.
- [x] Segment count and cost are logged for reporting.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
