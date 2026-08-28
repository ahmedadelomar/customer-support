# Story 26 — Agent tasks and timed reminders (Story: CS-403-tasks-reminders)

## Prerequisites

- CS-201 must be complete for the ticket link.
- `INotificationDispatcher` is declared; CS-504 implements the channels. Build against the interface now.

## Story Goal

Follow-up work stops depending on memory. Agents create tasks against tickets or customers, attach
reminders that fire once at the right time, and see everything due on their dashboard.

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/CS-403-tasks-reminders/intake.md`.
2. [backend/src/CustomerSupport.Domain/Workspace/AgentTask.cs](backend/src/CustomerSupport.Domain/Workspace/AgentTask.cs) — note it reuses `TicketPriority` so tasks and tickets sort consistently.
3. [backend/src/CustomerSupport.Domain/Workspace/Reminder.cs](backend/src/CustomerSupport.Domain/Workspace/Reminder.cs) — `Channels` is a comma-separated `NotificationChannel` list.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — `(IsSent, RemindAt)` is the dispatch index; `(AssignedToId, Status, DueAt)` is the list index.

## Product rules (from story)

- **Reminder dispatch is idempotent.** Claim rows with a conditional update before sending, so a job that overlaps itself cannot send twice.
- **A reminder in the past is rejected** at creation.
- **Snoozing creates a new reminder** rather than mutating the sent one, preserving the record that the first fired.
- **Closing a ticket with open linked tasks warns** rather than silently orphaning them.
- **Assigning a task to a colleague notifies them.**
- **Times are handled in the agent's time zone** but stored as UTC. A reminder set for "9am" must fire at the agent's 9am.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/tasks` | `workspace.tasks.view` | Own tasks by default; `assignedToId` requires `workspace.tasks.assign`. |
| POST/PUT/DELETE | `/api/tasks`, `/api/tasks/{id}` | `workspace.tasks.manage` | |
| POST | `/api/tasks/{id}/complete` | owner or `workspace.tasks.manage` | |
| POST | `/api/reminders/{id}/snooze` | owner | Body: `{ minutes }`. |
| POST | `/api/reminders/{id}/dismiss` | owner | |
| GET | `/api/tickets/{id}/tasks` | `tickets.view` | Tasks linked to a ticket. |

## Backend Tasks

### 1 — Task slice

**File:** `backend/src/CustomerSupport.Application/Workspace/Tasks/`

Standard CRUD. `GetTasksQuery` defaults to the caller's own tasks and only honours `assignedToId` when they hold `workspace.tasks.assign` — otherwise an agent could enumerate a colleague's workload.

Order overdue first, then by due date, then priority level descending.

`CompleteTaskCommand` sets `CompletedAt` and `CompletedById` and cancels any unsent reminders on the task: a reminder for a completed task is pure noise.

### 2 — Idempotent reminder dispatch

**File:** `backend/src/CustomerSupport.Infrastructure/Jobs/ReminderDispatchJob.cs`

Runs every minute. Claim before sending, so an overlapping run cannot double-send:

```csharp
var now = clock.UtcNow;

// Claim atomically: only rows this run flips from unsent to sent are ours to dispatch.
var claimed = await db.Reminders
    .Where(r => !r.IsSent && !r.IsDismissed && r.RemindAt <= now)
    .Take(200)
    .Select(r => r.Id)
    .ToListAsync(ct);

foreach (var id in claimed)
{
    var updated = await db.Reminders
        .Where(r => r.Id == id && !r.IsSent)
        .ExecuteUpdateAsync(s => s
            .SetProperty(r => r.IsSent, true)
            .SetProperty(r => r.SentAt, now), ct);

    if (updated == 0) continue;   // another instance already took it

    await dispatcher.DispatchAsync(...);
}
```

Marking sent **before** dispatching is deliberate: a duplicate reminder is worse than a missed one, and a failed dispatch is visible in the notification log.

Configure the Quartz trigger with `DisallowConcurrentExecution` as a second layer.

### 3 — Time-zone correct scheduling

Accept the reminder time as a local time plus the agent's IANA zone, convert to UTC on write, and validate against `clock.UtcNow` after conversion.

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId ?? branch.TimeZoneId ?? "Asia/Riyadh");
var utc = TimeZoneInfo.ConvertTimeToUtc(request.RemindAtLocal, zone);

if (utc <= clock.UtcNow)
{
    throw new ValidationException(new Dictionary<string, string[]>
    {
        ["remindAt"] = ["The reminder time must be in the future."],
    });
}
```

## Frontend Tasks

### 4 — Task list and composer

A dashboard section listing open tasks by due date, overdue in rose with an alert icon. A compact composer: title, due date-time, priority, optional reminder with channel checkboxes.

A tasks panel on the ticket screen showing linked tasks with an inline add, so creating a follow-up never means leaving the ticket.

A full `/agent/tasks` page with filters for status, due range and assignee (the last gated on the assign permission).

### 5 — Reminder toast with snooze

When a reminder notification arrives, show a persistent toast (no auto-dismiss) with the message, a link to the linked ticket, a **Snooze** menu (10m, 1h, 3h, tomorrow) and **Dismiss**.

Snooze posts to the snooze endpoint, which creates a new reminder — the fired one stays in the record.

### 6 — Warn on closing with open tasks

In the status change flow (CS-204), when moving to a terminal status, check for open linked tasks and show a confirm listing them, with the option to complete them all in the same action.

## Verification Steps

1. Create a task with a due date: it appears on the dashboard ordered correctly.
2. Create a task with a reminder 2 minutes out: it fires once, on time.
3. Run two dispatch job instances concurrently: the reminder still fires exactly once.
4. Set a reminder in the past: rejected with a field-level error.
5. Set a reminder for 9am in an agent time zone different from the server: it fires at their 9am.
6. Snooze a reminder for 10 minutes: a new reminder is created and the original stays marked sent.
7. Complete a task with an unsent reminder: the reminder is cancelled.
8. Assign a task to a colleague: they are notified and it appears in their list.
9. Try to list another agent's tasks without the assign permission: refused.
10. Close a ticket with two open linked tasks: a warning lists them and offers to complete them.

## Done Criteria

- [ ] Task CRUD, completion and ticket linking work behind their permissions.
- [ ] Reminder dispatch is idempotent under concurrent job runs.
- [ ] Times are stored in UTC and scheduled correctly for the agent's zone.
- [ ] Snooze creates a new reminder; completion cancels pending ones.
- [ ] Closing a ticket warns about open linked tasks.
- [ ] Tasks appear on the dashboard, the ticket screen and a dedicated page.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
