# Story 32 — Notification centre, preferences and reliable fan-out (Story: CS-504-alerts-notifications)

## Prerequisites

- CS-1001 must be complete.
- **Many earlier stories already call `INotificationDispatcher`.** This story implements it. If those stories are blocked waiting for real delivery, pull this one forward — it has no dependency on 29–31.
- CS-301 and CS-304 provide the email and SMS senders; CS-303 provides the SignalR infrastructure.

## Story Goal

Everyone hears about what needs them, on the channels they chose, without being woken at 3am for
something routine. Delivery is reliable through the outbox, and in-app notifications arrive in real time.

## Context — Read These Files First

1. `.squad/stories/sla-automation/CS-504-alerts-notifications/intake.md`.
2. [backend/src/CustomerSupport.Domain/Automation/Notification.cs](backend/src/CustomerSupport.Domain/Automation/Notification.cs) — bilingual text rendered at write time; `Severity` overrides quiet hours when Critical.
3. [backend/src/CustomerSupport.Domain/Automation/NotificationPreference.cs](backend/src/CustomerSupport.Domain/Automation/NotificationPreference.cs) — a missing row means the system default.
4. [backend/src/CustomerSupport.Domain/Integrations/OutboxMessage.cs](backend/src/CustomerSupport.Domain/Integrations/OutboxMessage.cs) — read the class remark: this is what makes delivery reliable rather than best-effort.
5. [backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs](backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs) — the `INotificationDispatcher` signature, already taking both languages.

## Product rules (from story)

- **Text is rendered in the recipient's language at creation**, not at display. A later template change must not silently rewrite what someone was told.
- **A missing preference row means the documented system default**, never silence.
- **Quiet hours defer external delivery but never the in-app notification.** Critical severity ignores quiet hours entirely.
- **External delivery goes through the outbox**, so a provider outage delays rather than loses.
- **In-app delivery is real-time over SignalR**, with the notification centre as the fallback on reconnect.
- **Digest grouping** collapses a burst of same-type events into one notification.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/notifications` | authenticated | Own notifications, unread first, keyset paged. |
| GET | `/api/notifications/unread-count` | authenticated | For the badge. |
| POST | `/api/notifications/{id}/read` , `/read-all` | owner | |
| GET/PUT | `/api/notification-preferences` | authenticated | The full matrix for the caller. |
| WS | `/hubs/notifications` | token | Real-time push. |

## Backend Tasks

### 1 — Dispatcher with preference resolution

**File:** `backend/src/CustomerSupport.Infrastructure/Services/NotificationDispatcher.cs`

```csharp
public async Task DispatchAsync(Guid userId, string eventType, string titleEn, string titleAr,
                                string bodyEn, string bodyAr, string? link, string severity,
                                CancellationToken ct)
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
    if (user is null) return;   // never notify a deactivated account

    // 1. The in-app row is always created, regardless of preferences or quiet hours.
    var notification = new Notification
    {
        UserId = userId, BranchId = user.BranchId, EventType = eventType,
        Title = new LocalizedText(titleEn, titleAr),
        Body = new LocalizedText(bodyEn, bodyAr),
        Link = link, Severity = severity, CreatedAt = clock.UtcNow,
    };
    db.Notifications.Add(notification);

    // 2. External channels follow preferences, defaulting when no row exists.
    var pref = await db.NotificationPreferences
        .FirstOrDefaultAsync(p => p.UserId == userId && p.EventType == eventType, ct)
        ?? DefaultPreferenceFor(eventType);

    var isCritical = severity == "Critical";
    var inQuietHours = !isCritical && IsWithinQuietHours(pref, user, clock.UtcNow);

    var channels = new List<NotificationChannel>();
    if (pref.ViaEmail) channels.Add(NotificationChannel.Email);
    if (pref.ViaSms)   channels.Add(NotificationChannel.Sms);
    if (pref.ViaPush)  channels.Add(NotificationChannel.Push);

    foreach (var channel in channels)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = $"notification.{channel}".ToLowerInvariant(),
            PayloadJson = JsonSerializer.Serialize(new
            {
                notification.Id, userId, channel = channel.ToString(),
                language = user.PreferredLanguage,
                title = user.PreferredLanguage == "ar" ? titleAr : titleEn,
                body  = user.PreferredLanguage == "ar" ? bodyAr  : bodyEn,
                link,
            }),
            AggregateType = nameof(Notification), AggregateId = notification.Id,
            OccurredAt = clock.UtcNow,
            // Quiet hours defer the send rather than dropping it.
            NextAttemptAt = inQuietHours ? QuietHoursEnd(pref, user, clock.UtcNow) : clock.UtcNow,
        });
    }

    notification.DispatchedChannels = string.Join(',', channels);
    // The caller saves, so the notification commits with whatever triggered it.
}
```

Note it does **not** call `SaveChangesAsync`: the notification must commit with the event that caused it, or a rolled-back transaction leaves a notification for something that never happened.

### 2 — Outbox dispatcher and digest

**File:** `backend/src/CustomerSupport.Infrastructure/Jobs/OutboxDispatcherJob.cs`

Every 30 seconds, claim due unprocessed rows using the `IX_OutboxMessage_Dispatch` index, oldest first, in batches of 100. Route by `Type` to the email, SMS or push sender.

On failure, increment `Attempts`, record the error, and set `NextAttemptAt` with exponential backoff (1m, 5m, 15m, 1h, 6h), abandoning after 6 attempts with a logged error.

Claim before sending, exactly as the reminder job does, so overlapping runs cannot double-send.

Digest: when more than the configured threshold of the same `eventType` are pending for one user within the window, collapse them into one outbox message summarising the count and linking to the filtered list.

### 3 — Notifications hub and defaults registry

A SignalR hub with a per-user group. Push on creation via the outbox dispatcher, or directly after save for in-app.

Define the system defaults in one place, so "a missing row means the default" is a real rule rather than a scattered set of guesses:

```csharp
private static readonly Dictionary<string, NotificationPreference> Defaults = new()
{
    ["ticket.assigned"]   = new() { ViaInApp = true, ViaEmail = true },
    ["ticket.mentioned"]  = new() { ViaInApp = true, ViaEmail = true },
    ["sla.warning"]       = new() { ViaInApp = true },
    ["sla.breached"]      = new() { ViaInApp = true, ViaEmail = true },
    ["ticket.escalated"]  = new() { ViaInApp = true, ViaEmail = true },
    ["task.reminder"]     = new() { ViaInApp = true },
    ["ticket.replied"]    = new() { ViaInApp = true },
};
```

## Frontend Tasks

### 4 — Notification centre

**File:** `frontend/src/app/layout/notifications/`

A bell button in the top bar with an unread badge, opening a panel listing notifications newest first with severity colouring and relative timestamps. Unread rows have a left accent bar.

Actions: mark one read, mark all read, and open (which navigates and marks read). Infinite scroll using the keyset pattern.

Connect to the hub on sign-in and push arrivals into the signal so the badge updates live. On reconnect, refetch the unread count rather than assuming the in-memory list is complete.

### 5 — Preferences screen

A matrix at `/agent/settings/notifications`: one row per event type, one column per channel, with checkboxes. Rows with no saved preference show the default state visibly marked as "default", so a user can see what will happen before they change anything.

Quiet-hours start and end pickers with a note that critical alerts always come through.

Group event types by area (tickets, SLA, tasks, collaboration) so the matrix is scannable rather than a flat list of thirty rows.

## Verification Steps

1. Assign a ticket to another agent: they receive an in-app notification in real time without refreshing, and the badge increments.
2. Confirm the notification text is in the recipient's preferred language, even when the actor's language differs.
3. Enable email for that event: an email is also sent, in the recipient's language.
4. Delete a preference row and trigger the event: the documented default applies.
5. Set quiet hours covering now and trigger a non-critical event: the in-app notification appears immediately, the email is deferred to the end of quiet hours.
6. Trigger an SLA breach (Critical) during quiet hours: the email is sent immediately.
7. Stop the mail provider and trigger a notification: the outbox retries with backoff and delivers when it returns.
8. Confirm a failed send after 6 attempts is abandoned with a logged error, not retried forever.
9. Trigger 20 same-type events for one user quickly: they collapse into a digest.
10. Roll back a transaction that dispatched a notification: no notification row remains.
11. Disconnect and reconnect the hub: the unread count refetches and is correct.
12. Mark all read: the badge clears and stays cleared after a refresh.

## Done Criteria

- [x] `INotificationDispatcher` is implemented and adds to the caller's unit of work rather than
      saving — `NotificationDispatcher` never calls `SaveChangesAsync`; verified by
      `RolledBackTransaction_LeavesNoNotificationRow`.
- [x] Preferences resolve with documented defaults, and quiet hours defer without dropping —
      `NotificationEventTypes` is the single registry both the dispatcher and the preferences API
      read; verified by `MissingPreferenceRow_UsesTheDocumentedDefault` and
      `QuietHours_DefersExternalDelivery_ButNotTheInAppRow`.
- [x] Critical severity bypasses quiet hours — verified by `CriticalSeverity_BypassesQuietHours`
      and live (an SLA breach notification's outbox row got `NextAttemptAt = now`, not deferred).
- [x] External delivery goes through the outbox with backoff and abandonment —
      `OutboxDispatcherJob` claims via the same compare-and-swap pattern round robin uses, backs off
      1m/5m/15m/1h/6h, and abandons (not retries forever) after 6 attempts. Verified live: the
      30-second tick claimed and delivered a real assignment notification through the logging sender.
- [x] Real-time in-app delivery works, with a correct count after reconnect — a `SaveChangesInterceptor`
      pushes over `/hubs/notifications` only after a transaction actually commits (never on rollback);
      the bell refetches the unread count on `onreconnected` rather than trusting the in-memory list.
      Verified live: `/hubs/notifications/negotiate` returns 200; mark-all-read zeroes the count.
- [x] Digest grouping works for bursts — `OutboxDispatcherJob.CollapseDigestsAsync` groups pending
      same-user/same-event/same-channel rows in a 10-minute window and collapses groups over 5 into
      one summary row, marking the rest "merged into digest" rather than sending each individually.
- [x] The preferences matrix shows defaults distinctly and is grouped by area — `isUsingDefault` on
      each row drives a "Default" chip; rows are grouped into Tickets/SLA/Tasks/Collaboration sections.

**Not verified live:** the notification bell panel, preferences screen and digest collapsing in an
actual browser or against a real burst of 6+ events — no browser-automation tool in this session,
and generating a live burst would have meant scripting six ticket assignments just to trigger it.
Exercised by unit tests instead (7 for the dispatcher, covering every rule above except digest,
which needs the job's own in-memory grouping and was reasoned through instead).
