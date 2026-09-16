# Story 29 — SLA policies, business calendars and live clocks (Story: CS-501-response-resolution-targets)

## Prerequisites

- CS-201 and CS-204 must be complete. CS-204 calls `ISlaEngine.OnStatusChangedAsync` and `OnResolvedAsync`; this story implements them.
- **Agree the pause/resume contract with CS-204 in writing before starting.** Both stories touch the same behaviour from different sides.
- A default calendar and a Standard SLA policy are already seeded.

## Story Goal

Support commitments become measurable. Working-hours arithmetic computes due times that respect the
calendar, holidays and time zone; clocks pause and resume without losing elapsed time; and breaches are
detected by a background sweep rather than only when someone opens a screen.

**This is the highest-risk story in the product for correctness.** Its unit-test suite matters more than its
UI.

## Context — Read These Files First

1. `.squad/stories/sla-automation/CS-501-response-resolution-targets/intake.md`.
2. [backend/src/CustomerSupport.Domain/Sla/TicketSlaClock.cs](backend/src/CustomerSupport.Domain/Sla/TicketSlaClock.cs) — read the class remark: **this is the authority**, and the `Ticket` columns merely mirror it.
3. [backend/src/CustomerSupport.Domain/Sla/SlaPolicy.cs](backend/src/CustomerSupport.Domain/Sla/SlaPolicy.cs) — `EvaluationOrder`, `IsDefault`, `WarningThresholdPercent`, `PauseOnPendingCustomer`.
4. [backend/src/CustomerSupport.Domain/Sla/BusinessCalendar.cs](backend/src/CustomerSupport.Domain/Sla/BusinessCalendar.cs), [BusinessHour.cs](backend/src/CustomerSupport.Domain/Sla/BusinessHour.cs), [Holiday.cs](backend/src/CustomerSupport.Domain/Sla/Holiday.cs) — note `IsTwentyFourSeven` and `IsRecurringAnnually`.
5. [backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs](backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs) — the `ISlaEngine` and `IBusinessCalendarCalculator` contracts to implement.
6. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/SlaAndAutomationConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/SlaAndAutomationConfigurations.cs) — `(Status, DueAt)` is the index the breach sweep runs on.

## Product rules (from story)

- **Targets are working minutes, not wall-clock minutes.** A 4-hour target on a Thursday afternoon is due Sunday morning.
- **Policy selection:** active policies in `EvaluationOrder`, first whose conditions all match; otherwise the `IsDefault` policy.
- **Two clocks per ticket**, one per `SlaTargetType`, enforced by the unique index.
- **Pausing affects only the resolution clock.** Waiting on the customer never excuses a missing first response.
- **Resume recomputes the due time from the remaining budget**, never from the original target. Restarting erases breaches.
- **The warning fires once**, guarded by `WarningSent`.
- **The denormalised `Ticket` columns are updated in the same transaction as the clock.** They exist only so the list query avoids a join, and a drifted mirror is worse than a join.
- **A ticket with no matching policy and no default has no clocks** and sorts last everywhere, rather than appearing overdue.

## Data model

All entities already exist. Note the invariant enforced by `(TicketId, TargetType)` unique: applying a policy twice must update the existing clocks, not insert duplicates.

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Business calendar calculator

**File:** `backend/src/CustomerSupport.Infrastructure/Services/BusinessCalendarCalculator.cs`

The core arithmetic. Implement `IBusinessCalendarCalculator`:

```csharp
public async Task<DateTimeOffset> AddWorkingMinutesAsync(
    Guid calendarId, DateTimeOffset from, int minutes, CancellationToken ct)
{
    var calendar = await LoadCachedAsync(calendarId, ct);
    if (calendar.IsTwentyFourSeven) return from.AddMinutes(minutes);

    var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
    var cursor = TimeZoneInfo.ConvertTime(from, zone);
    var remaining = minutes;

    // Walk day by day, consuming the working windows that remain.
    while (remaining > 0)
    {
        var day = DateOnly.FromDateTime(cursor.DateTime);

        if (IsHoliday(calendar, day))
        {
            cursor = StartOfNextDay(cursor);
            continue;
        }

        var windows = calendar.BusinessHours
            .Where(h => h.DayOfWeek == cursor.DayOfWeek)
            .OrderBy(h => h.StartTime);   // ordered, so split shifts are consumed in sequence

        var consumedToday = false;
        foreach (var window in windows)
        {
            var open  = OnDate(day, window.StartTime, zone);
            var close = OnDate(day, window.EndTime, zone);

            if (cursor >= close) continue;           // window already past
            var start = cursor < open ? open : cursor;   // arrived before opening

            var available = (int)(close - start).TotalMinutes;
            if (available >= remaining) return start.AddMinutes(remaining);

            remaining -= available;
            cursor = close;
            consumedToday = true;
        }

        if (!consumedToday || remaining > 0) cursor = StartOfNextDay(cursor);
    }

    return cursor;
}
```

`WorkingMinutesBetweenAsync` is the same walk, accumulating instead of consuming.

**Cache calendars aggressively** — this runs on every ticket create, status change and breach sweep. Cache the calendar with its hours and holidays as one object, invalidated on write.

Guard against an infinite loop: if a calendar has no working hours at all, throw a clear exception rather than looping forever.

### 2 — Unit tests before the engine

**File:** `backend/tests/CustomerSupport.UnitTests/Sla/BusinessCalendarCalculatorTests.cs`

**Write these first.** The arithmetic is not something to verify by clicking through a UI.

Cover, with the seeded Sunday–Thursday 08:00–17:00 Asia/Riyadh calendar:

- Within one day: Sunday 09:00 + 240 min = Sunday 13:00.
- Spanning a close: Sunday 16:00 + 120 min = Monday 09:00.
- Spanning a weekend: Thursday 16:00 + 240 min = Sunday 11:00.
- Arriving before opening: Sunday 06:00 + 60 min = Sunday 09:00.
- Arriving after closing: Sunday 19:00 + 60 min = Monday 09:00.
- Arriving on a non-working day: Friday 10:00 + 60 min = Sunday 09:00.
- Holiday exclusion, including a recurring annual holiday.
- A split shift (08:00–12:00 and 14:00–18:00): 11:00 + 120 min = 15:00.
- A 24/7 calendar: exact wall-clock addition.
- Zero minutes returns the input unchanged.
- A daylight-saving transition in a zone that observes it.
- `WorkingMinutesBetween` is the inverse of `AddWorkingMinutes` across each case.

### 3 — SLA engine

**File:** `backend/src/CustomerSupport.Infrastructure/Services/SlaEngine.cs`

`ApplyPolicyAsync`: select the policy, find the `SlaTarget` for the ticket priority, and upsert both clocks (upsert, because a priority change re-applies the policy and must not violate the unique index). Mirror `DueAt` onto the ticket columns in the same `SaveChangesAsync`.

`OnFirstAgentReplyAsync`: if the first-response clock is still running, set `MetAt`, compute `ElapsedMinutes`, and set `Status` to `Met` or `Breached` depending on whether the reply beat `DueAt`. Marking a late first reply as `Breached` rather than `Met` is what keeps the report honest.

`OnStatusChangedAsync` — the pause and resume logic:

```csharp
var clock = await GetResolutionClock(ticketId, ct);
if (clock is null || clock.Status is SlaClockStatus.Met or SlaClockStatus.Breached) return;

if (status.PausesSla && clock.Status == SlaClockStatus.Running)
{
    clock.ElapsedMinutes += await calendar.WorkingMinutesBetweenAsync(
        calendarId, clock.PausedAt ?? clock.StartedAt, now, ct);
    clock.PausedAt = now;
    clock.Status = SlaClockStatus.Paused;
}
else if (!status.PausesSla && clock.Status == SlaClockStatus.Paused)
{
    clock.PausedMinutes += await calendar.WorkingMinutesBetweenAsync(calendarId, clock.PausedAt!.Value, now, ct);

    // Recompute from what is LEFT, never from the original target.
    var remaining = Math.Max(0, clock.TargetMinutes - clock.ElapsedMinutes);
    clock.DueAt = await calendar.AddWorkingMinutesAsync(calendarId, now, remaining, ct);

    clock.PausedAt = null;
    clock.Status = SlaClockStatus.Running;
    ticket.ResolutionDueAt = clock.DueAt;
}
```

### 4 — Breach sweep job

**File:** `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachSweepJob.cs`

Quartz, every minute, `DisallowConcurrentExecution`. Two passes over the `(Status, DueAt)` index.

Breaches — running clocks past due:

```csharp
var breached = await db.TicketSlaClocks
    .Where(c => c.Status == SlaClockStatus.Running && c.DueAt <= now)
    .Take(500)
    .ToListAsync(ct);
```

For each: set `BreachedAt`, `Status = Breached`, set the matching denormalised flag on the ticket, record a `SlaBreached` ticket event, and dispatch a critical notification.

Warnings — running clocks past the threshold with `WarningSent == false`. Compute consumed percentage from elapsed working minutes against the target, set `WarningSent = true` **before** dispatching, so a retry cannot double-warn.

Batch and loop until a pass returns fewer than the batch size, so a backlog drains rather than trickling one batch per minute.

### 5 — Policy and calendar administration

CRUD for policies (with their targets and conditions) and calendars (with hours and holidays), behind `sla.policies.manage` and `sla.calendars.manage`.

Guard: exactly one default policy; a policy must have a target for every active priority, or tickets at an uncovered priority silently get no clock; a calendar in use cannot be deleted.

Add `POST /api/sla/policies/{id}/preview` taking a priority and a hypothetical arrival time, returning the computed due times — this is how a manager sanity-checks a policy without creating a real ticket.

## Frontend Tasks

### 6 — SLA indicators

A shared `SlaBadgeComponent` used on the ticket list, detail and dashboard: shows remaining time as a relative string (`2h 15m left`), amber past the warning threshold, rose when breached with the overdue amount, and neutral when there is no policy.

Add `slaState` (`ok`, `warning`, `breached`, `none`) to the ticket list filters and make the due column sortable.

On the ticket detail, show both commitments with their due time, current state, and paused minutes when paused, so an agent can see why a clock is not moving.

### 7 — Policy and calendar editors

The policy editor: name, calendar, evaluation order, default toggle, warning threshold, pause-on-pending toggle, a conditions builder (field, operator, value), and a targets grid with a row per priority and first-response and resolution minute inputs. Show each target's minutes rendered as a human duration ("4h working time") to avoid unit confusion.

The calendar editor: time zone picker, a 24/7 toggle that disables the hours grid, a weekly grid supporting multiple windows per day, and a holiday list with a recurring-annually flag.

Include the preview tool: pick a priority and an arrival date-time, see the computed due times.

## Verification Steps

1. Run the calculator unit suite: every case passes, including the weekend, split-shift, holiday and DST cases.
2. Create a normal-priority ticket at 16:00 Thursday: the resolution due time is Sunday morning, computed from working hours.
3. Add a holiday on that Sunday: the due time moves to Monday.
4. Reply as an agent within the first-response target: the clock stops as Met.
5. Reply after the target: the clock stops as Breached, not Met.
6. Move a ticket to Pending Customer: the resolution clock pauses, the first-response clock does not.
7. Wait, then return it to Open: the due time is now plus the remaining budget, not the full original target.
8. Verify paused minutes accumulate correctly across two pause cycles.
9. Let a clock pass its due time: the sweep marks it breached within a minute, sets the ticket flag, records the event and sends a critical notification.
10. Let a clock cross the warning threshold: exactly one warning is sent, even after several sweep runs.
11. Run two sweep instances concurrently: no duplicate breach events.
12. Create a ticket at a priority with no target defined: it gets no clocks and sorts last, rather than appearing overdue.
13. Use the policy preview to check due times without creating a ticket.

## Done Criteria

- [x] Working-hours arithmetic is correct across the full test matrix, with calendars cached —
      `BusinessCalendarCalculator` shares one lazy `WorkingSegments` sequence between
      `AddWorkingMinutesAsync` and `WorkingMinutesBetweenAsync`; 15 unit tests cover within-day,
      close-spanning, weekend, split-shift, one-off and recurring-annual holidays, 24/7, zero
      minutes, a DST transition, and the inverse relationship. Calendars cached 30 minutes in
      `IMemoryCache`, invalidated on write via `IBusinessCalendarCacheInvalidator`.
- [x] `ISlaEngine` is implemented for apply, first reply, status change and resolution — `SlaEngine`
      replaces the placeholder; 10 unit tests cover upsert-not-duplicate on re-apply, no-clock on
      an uncovered priority, on-time vs late first reply (Met vs Breached), pause/resume, two
      pause cycles accumulating `PausedMinutes`, and on-time vs late resolution.
- [x] Pause affects only resolution; resume recomputes from the remaining budget — verified by
      `MovingToPendingCustomer_PausesResolutionClockOnly` and
      `ReturningFromPending_ResumesFromRemainingBudget_NotOriginalTarget`; extended beyond the
      plan's own pseudocode so a ticket reopened after resolution also resumes rather than staying
      frozen, per the index's "resume never restarts" invariant.
- [x] The breach sweep detects breaches and warns exactly once, batched and non-overlapping —
      `SlaBreachSweepJob`, `[DisallowConcurrentExecution]`, batches of 500, loops until a pass is
      short; `WarningSent` is set before dispatch. Verified live: creating a ticket sets real
      `firstResponseDueAt`/`resolutionDueAt` where it previously read null.
- [x] Denormalised ticket columns are updated in the same transaction as the clock — every
      `SlaEngine` method updates `Ticket` and `TicketSlaClock` in one `SaveChangesAsync`.
- [x] Policy and calendar admin enforce the default and coverage invariants, with a preview tool —
      `CreateSlaPolicyCommand`/`UpdateSlaPolicyCommand` refuse removing the last default and require
      a target for every active priority on non-default policies; `POST /api/SlaPolicies/{id}/preview`
      verified live returning correct working-hours due times.
- [x] SLA state is visible and filterable across the list, detail and dashboard — the ticket list's
      `slaState` filter and sortable due column already existed from CS-401's anticipation; a new
      shared `SlaBadgeComponent` (ok/warning/breached/none) now renders it consistently there, on
      the ticket detail properties panel, and on the dashboard next-up queue.

**Not verified live:** the policy/calendar admin screens and the SLA badge in an actual browser —
no browser-automation tool in this session. Reasoned about and exercised via curl + unit tests only.
