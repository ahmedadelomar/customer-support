# Story 24 — Agent dashboard and the next-up queue (Story: CS-401-assigned-tickets)

## Prerequisites

- CS-201 must be complete.
- CS-501 gives real SLA data. Without it, build the ordering with the denormalised columns unpopulated and confirm it degrades sensibly.
- `DashboardPage` already exists as a placeholder with static tiles — replace its contents.

## Story Goal

An agent opens the dashboard and immediately knows what to work on. Tiles summarise their load, and
the next-up queue is ordered by actual urgency rather than by age.

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/CS-401-assigned-tickets/intake.md`.
2. [frontend/src/app/features/agent/dashboard/dashboard.page.ts](frontend/src/app/features/agent/dashboard/dashboard.page.ts) — the placeholder to replace.
3. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — the denormalised SLA columns exist so this query needs no join.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs) — `(AssignedAgentId, StatusId, CreatedAt)` is the agent-queue index.
5. [frontend/src/app/shared/ui/state-card/state-card.component.ts](frontend/src/app/shared/ui/state-card/state-card.component.ts) — the KPI tile.

## Product rules (from story)

- **Order by urgency, not age:** breached first, then approaching breach by remaining time, then priority level descending, then oldest.
- **Every tile links to the ticket list with the same filter**, so the dashboard is a starting point rather than a dead end.
- **Auto-refresh must not disturb the user** — no scroll jump, no closed menu, no lost draft.
- **Personal statistics are context, not a leaderboard.** Show the team average beside the agent's own figure; do not rank agents against each other on their own dashboard.
- **Every section is permission-gated** and simply absent when not permitted.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/dashboard/agent` | authenticated | Tiles, next-up queue and personal stats in one response. |
| GET | `/api/dashboard/agent/queue` | `tickets.view` | Just the queue, for the refresh poll. |

## Backend Tasks

### 1 — Dashboard query

**File:** `backend/src/CustomerSupport.Application/Dashboard/Queries/GetAgentDashboardQuery.cs`

One request, one response — a dashboard that fires six requests on load is slow on a poor connection.

Compute the tiles in a single grouped query rather than five counts:

```csharp
var now = clock.UtcNow;
var endOfDay = now.Date.AddDays(1);

var mine = db.Tickets
    .WhereBranchAccessible(currentUser)
    .Where(t => t.AssignedAgentId == currentUser.UserId && !t.Status.IsTerminal);

var tiles = await mine
    .GroupBy(_ => 1)
    .Select(g => new AgentDashboardTilesDto
    {
        Assigned      = g.Count(),
        DueToday      = g.Count(t => t.ResolutionDueAt != null && t.ResolutionDueAt < endOfDay),
        ApproachingSla= g.Count(t => !t.IsResolutionBreached && t.ResolutionDueAt != null
                                     && t.ResolutionDueAt > now && t.ResolutionDueAt < now.AddHours(2)),
        Breached      = g.Count(t => t.IsResolutionBreached || t.IsFirstResponseBreached),
    })
    .FirstOrDefaultAsync(ct) ?? new AgentDashboardTilesDto();
```

The next-up queue applies the urgency ordering:

```csharp
var queue = await mine
    .OrderByDescending(t => t.IsResolutionBreached || t.IsFirstResponseBreached)
    .ThenBy(t => t.ResolutionDueAt ?? DateTimeOffset.MaxValue)   // nulls last
    .ThenByDescending(t => t.Priority.Level)
    .ThenBy(t => t.CreatedAt)
    .Take(10)
    .Select(...)
    .ToListAsync(ct);
```

Note the `?? MaxValue`: tickets with no SLA must sort last, not first, which is what a raw null ordering would do on some providers.

Resolved-this-week and the team average come from `TicketDailyMetric` (CS-901) when available, falling back to a direct count until then.

## Frontend Tasks

### 2 — Dashboard page

**File:** [frontend/src/app/features/agent/dashboard/dashboard.page.ts](frontend/src/app/features/agent/dashboard/dashboard.page.ts)

Replace the placeholder. Five tiles, each a router link carrying the matching query params, for example:

```ts
{ labelKey: 'dashboard.tiles.breached', value: tiles().breached, icon: '⚠',
  badgeClasses: 'bg-rose-50 text-rose-700',
  link: ['/agent/tickets'], params: { assignment: 'mine', slaState: 'breached' } }
```

Below, the next-up queue as compact rows: number, subject, customer, priority chip, and a due-time badge that is red when past and amber within the warning threshold. Row actions: **Open**, and **Claim** when unassigned.

Personal stats as a small comparison strip: "You resolved 23 this week · team average 19".

### 3 — Non-disruptive auto-refresh

Poll `/api/dashboard/agent/queue` on an interval from a signal-based timer, and **skip the refresh while a menu is open or the tab is hidden**:

```ts
// Refreshing while a row menu is open closes it under the user's cursor.
if (this.#document.hidden || this.openMenuRowId() !== null) return;
```

Update the queue signal in place rather than replacing the component, so scroll position survives. Pause polling entirely when the tab is backgrounded and refresh once on focus — an unattended dashboard should not poll all night.

### 4 — Empty and permission states

With no assigned tickets, show an empty state offering **View my team's unassigned queue** rather than a blank panel.

Wrap the stats section in `*hasPermission="permissions.reports.viewAgentPerformance"` and the queue in `tickets.view`, so a restricted role sees a coherent smaller dashboard rather than broken sections.

## Verification Steps

1. Open the dashboard: all five tiles show correct counts for the signed-in agent.
2. Click each tile: the ticket list opens with the matching filter applied.
3. Confirm the queue orders breached first, then by remaining time, then priority, then age.
4. Create a ticket with no SLA policy: it sorts last, not first.
5. Leave the dashboard open past the refresh interval: counts update without the page jumping.
6. Open a row action menu and wait for a refresh: the menu stays open.
7. Switch to another browser tab for a minute: polling pauses, and refreshes once on return.
8. Sign in as an agent with no assignments: the empty state offers the team queue.
9. Sign in as a role without `reports.agents.view`: the stats strip is absent and the rest still renders.
10. Check the dashboard on a phone: tiles stack and the queue renders as cards.

## Done Criteria

- [ ] One request populates tiles, queue and stats.
- [ ] Tiles are computed in a single grouped query and each links to the filtered list.
- [ ] Urgency ordering is correct, including null-SLA tickets sorting last.
- [ ] Auto-refresh never disturbs scroll, open menus or drafts, and pauses on a hidden tab.
- [ ] Empty and permission-restricted states are coherent.
- [ ] Responsive and translated.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
