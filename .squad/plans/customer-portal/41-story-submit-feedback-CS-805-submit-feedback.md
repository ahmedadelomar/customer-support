# Story 41 — Satisfaction surveys and feedback (Story: CS-805-submit-feedback)

## Prerequisites

- CS-802 must be complete.
- CS-204 queues the survey on resolution — that call must exist before this story can be tested end to end.
- CS-301 and CS-304 provide delivery; CS-504 provides the manager notification.

## Story Goal

Every resolved ticket asks the customer whether it was actually resolved well, through a link that needs
no sign-in, and a bad answer creates follow-up work rather than sitting in a report.

## Context — Read These Files First

1. `.squad/stories/customer-portal/CS-805-submit-feedback/intake.md`.
2. [backend/src/CustomerSupport.Domain/Portal/CsatSurvey.cs](backend/src/CustomerSupport.Domain/Portal/CsatSurvey.cs) — note the remark on `AgentId` being captured at send time, and the unique token.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — `TicketId` is **unique**: one survey per ticket.
4. [backend/src/CustomerSupport.Domain/Customers/Customer.cs](backend/src/CustomerSupport.Domain/Customers/Customer.cs) — `SatisfactionScore` is maintained from responses.

## Product rules (from story)

- **One survey per ticket**, enforced by a unique index. A resolve-reopen-resolve cycle updates the existing row rather than inserting a second.
- **The credited agent is captured at send time.** A later reassignment must not move a score onto someone who was not involved.
- **Tokens are single-use and expire** after `csat.surveyExpiryDays`.
- **A reopened ticket cancels its outstanding survey** — asking someone to rate an unresolved ticket is worse than not asking.
- **A low score creates a linked follow-up ticket and notifies the manager.** A bad rating that only appears in a monthly report has helped nobody.
- **Reminders are capped** at the configured count.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Survey creation and lifecycle

**File:** `backend/src/CustomerSupport.Application/Portal/Csat/`

`QueueCsatSurveyCommand`, called from the CS-204 resolution branch, upserts rather than inserts:

```csharp
var survey = await db.CsatSurveys.FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);

if (survey is { RespondedAt: not null }) return;   // already rated; do not ask again

survey ??= new CsatSurvey { TicketId = ticketId, CustomerId = ticket.CustomerId };

survey.AgentId = ticket.AssignedAgentId;   // captured now, so later reassignment cannot move the credit
survey.Token = GenerateUrlSafeToken(32);
survey.Language = customer.PreferredLanguage;
survey.SentAt = clock.UtcNow;
survey.ExpiresAt = clock.UtcNow.AddDays(await settings.GetAsync("csat.surveyExpiryDays", 14, ct));
survey.ReminderCount = 0;
```

Reopening cancels: set `ExpiresAt = UtcNow` on any unanswered survey for the ticket, in the CS-204 reopen path.

`SubmitCsatResponseCommand` (anonymous, token-authenticated) validates the token is unused and unexpired, records the score, recommendation score and comment, recomputes `Customer.SatisfactionScore` as the mean of responded surveys, and — when the score is at or below the configured threshold — creates a linked follow-up ticket at high priority and notifies the department manager.

### 2 — Reminder job

Daily: find surveys sent, unanswered, unexpired, at least 3 days since the last contact, with `ReminderCount` below the cap. Send through the customer's preferred channel, increment the counter, and stop at the cap.

Use the same claim-then-send pattern as the reminder job in CS-403 so an overlapping run cannot double-send.

## Frontend Tasks

### 3 — Survey response page

A public route `/feedback/{token}` needing no sign-in. A large 1-to-5 rating control with clear labels (not just stars — stars alone are ambiguous across cultures), an optional 0-to-10 recommendation scale, and a comment box.

Render in the survey's language with the correct direction, regardless of the browser locale.

Expired or used tokens show a clear, friendly message rather than an error page. After responding, thank the customer and link back to the help centre.

### 4 — Portal feedback surfaces

Show the rating inline on a resolved ticket in **My requests** for signed-in customers, so they can rate without finding the email.

Add a general feedback form under `/portal/feedback` for comments unrelated to a ticket, creating a ticket in a Feedback category so it reaches someone rather than a void.

## Verification Steps

1. Resolve a ticket: a survey is created and sent in the customer's language through their preferred channel.
2. Open the link without signing in: the survey renders in the right language and direction.
3. Submit a rating: it is recorded and the token cannot be reused.
4. Open an expired token: a clear message, not an error.
5. Reassign the ticket after the survey was sent, then respond: the score credits the originally recorded agent.
6. Submit a low score: a linked follow-up ticket is created at high priority and the manager is notified.
7. Reopen a ticket with an outstanding survey: the survey is cancelled and the link stops working.
8. Resolve it again: the existing survey row is reused, not duplicated — confirm the unique index is never violated.
9. Leave a survey unanswered: reminders are sent up to the cap and then stop.
10. Respond to several surveys for one customer: their profile satisfaction score is the mean of the responses.
11. Confirm the scores appear in the satisfaction report (CS-904) once that story lands.

## Done Criteria

- [ ] One survey per ticket, upserted rather than duplicated across resolve cycles.
- [ ] The credited agent is captured at send time.
- [ ] Tokens are single-use, expiring and usable without sign-in.
- [ ] Reopening cancels an outstanding survey.
- [ ] Low scores create linked follow-up tickets and notify the manager.
- [ ] Reminders are capped and cannot double-send.
- [ ] Customer satisfaction score is recomputed from responses.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
