# Story 47 — Daily aggregation and ticket reports (Story: CS-901-ticket-reports)

## Prerequisites

- CS-201 must be complete; CS-1203 and CS-1204 provide the organisational dimensions.
- `TicketDailyMetric` already exists with a unique index over its dimension tuple — the aggregation upserts against it.

## Story Goal

Build the measurement foundation: an idempotent nightly rollup plus a query layer that answers volume,
resolution and backlog questions over any range and grouping, fast.

## Context — Read These Files First

1. `.squad/stories/reports-management/CS-901-ticket-reports/intake.md`.
2. [backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs](backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs) — read the class remark and note the comment on storing sums **and** counts so averages recombine.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — `UX_TicketDailyMetric_Dimensions` is the upsert target.
4. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — the source columns the rollup reads.

## Product rules (from story)

- **Aggregation is idempotent per day.** Re-running after a backfill must replace, not add.
- **A null dimension means "aggregated across that dimension".** Write both the fully-dimensioned rows and the rolled-up combinations the reports actually query.
- **Averages come from sums over counts**, never from averaging averages.
- **Today is included via an incremental pass**, so a manager checking at 4pm sees today, not only yesterday.
- **No data is not zero.** The API must distinguish them and the UI must render them differently.
- **Reports read the rollup**, not the ticket table — except where a metric genuinely cannot be aggregated (medians, in story 48).

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/reports/tickets/volume` | `reports.tickets.view` | `from`, `to`, `groupBy`, plus dimension filters. |
| GET | `/api/reports/tickets/backlog` | `reports.tickets.view` | Backlog at each period end. |
| GET | `/api/reports/tickets/trend` | `reports.tickets.view` | Current versus previous period. |
| GET | `/api/reports/tickets/export` | `reports.export` | Xlsx or Csv. Audited. |
| POST | `/api/reports/aggregation/rebuild` | `admin.settings.manage` | Backfills a date range. |

## Backend Tasks

### 1 — Idempotent aggregation job

**File:** `backend/src/CustomerSupport.Infrastructure/Jobs/TicketMetricsAggregationJob.cs`

Runs nightly for the previous day, and hourly for the current day.

Delete then insert per day is the simplest correct idempotency — an upsert across a nine-column dimension tuple is error-prone:

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);

// Replace the day wholesale so a re-run after a backfill cannot double-count.
await db.TicketDailyMetrics.Where(m => m.Date == date).ExecuteDeleteAsync(ct);

var rows = await db.Tickets
    .Where(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEnd
                || t.ResolvedAt >= dayStart && t.ResolvedAt < dayEnd
                || t.ClosedAt >= dayStart && t.ClosedAt < dayEnd)
    .GroupBy(t => new
    {
        t.BranchId, t.DepartmentId, t.AssignedTeamId, t.AssignedAgentId,
        t.CategoryId, t.PriorityId, Channel = (int?)t.Channel,
    })
    .Select(g => new TicketDailyMetric
    {
        Date = date,
        BranchId = g.Key.BranchId, DepartmentId = g.Key.DepartmentId,
        TeamId = g.Key.AssignedTeamId, AgentId = g.Key.AssignedAgentId,
        CategoryId = g.Key.CategoryId, PriorityId = g.Key.PriorityId, Channel = g.Key.Channel,

        CreatedCount  = g.Count(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEnd),
        ResolvedCount = g.Count(t => t.ResolvedAt >= dayStart && t.ResolvedAt < dayEnd),
        ...
        // Sums, not averages — averages of averages are wrong under regrouping.
        TotalFirstResponseMinutes = g.Sum(t => t.FirstRespondedAt == null ? 0
            : (long)EF.Functions.DateDiffMinute(t.CreatedAt, t.FirstRespondedAt.Value)),
        ComputedAt = clock.UtcNow,
    })
    .ToListAsync(ct);

db.TicketDailyMetrics.AddRange(rows);
await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

Add a backfill entry point taking a date range and iterating day by day, so historical data can be built after deployment and rebuilt after a data fix.

**Note the trade-off explicitly in a comment:** this writes fully-dimensioned rows only. Queries that group by a subset must sum across rows — which the sums-and-counts design makes correct. Do not add pre-rolled-up rows unless query performance demands it, because they double the idempotency surface.

### 2 — Report query layer

**File:** `backend/src/CustomerSupport.Application/Reporting/Queries/`

`GetTicketVolumeReportQuery` takes a range, a `groupBy` from an allow-list, and dimension filters. Group the rollup rows and recombine:

```csharp
var rows = await query
    .GroupBy(m => selector(m))     // by day, week, month, or a dimension
    .Select(g => new ReportRowDto
    {
        Key = g.Key,
        Created = g.Sum(m => m.CreatedCount),
        Resolved = g.Sum(m => m.ResolvedCount),
        // Correct: total minutes over total count. Never Average(m => m.Avg…).
        AvgFirstResponseMinutes = g.Sum(m => m.FirstResponseMetCount + m.FirstResponseBreachedCount) == 0
            ? null
            : (double)g.Sum(m => m.TotalFirstResponseMinutes)
              / g.Sum(m => m.FirstResponseMetCount + m.FirstResponseBreachedCount),
        HasData = g.Any(),
    })
    .ToListAsync(ct);
```

Note `AvgFirstResponseMinutes` is nullable — null means no measurement, which the UI must render as an em dash rather than as zero.

Include today by unioning a live query over today's tickets with the stored rows, so the current day is never missing.

### 3 — Export

Excel export via a spreadsheet library (ClosedXML or equivalent), matching the on-screen columns, with headers in the requested language and RTL sheet direction for Arabic. Cap at 100,000 rows with a clear error above it. Write an `Export` audit entry (CS-1003).

## Frontend Tasks

### 4 — Report page and chart conventions

**File:** `frontend/src/app/features/agent/reports/`

A shared report shell: date-range picker with presets, a grouping selector, dimension filters, and a view toggle between chart and table.

Establish the charting conventions once here, since every later report reuses them: a consistent categorical palette, axis labels in the active language, locale-formatted numbers and dates, mirrored axes in RTL, and an explicit empty state.

**Render null as an em dash, never as 0.** A zero average response time is not a real measurement and reads as excellent performance.

## Verification Steps

1. Run the aggregation for a day with known ticket activity: the metrics match a manual count.
2. Run it again for the same day: the numbers are unchanged, not doubled.
3. Backfill 90 days: rows exist for each day with activity.
4. Open the volume report over 12 months grouped by month: it returns quickly.
5. Group by category, then by agent: the totals match in both groupings.
6. Verify a grouped average by hand against the underlying tickets — this is the check that catches averaged averages.
7. Check the report at 4pm: today's tickets are included.
8. Filter by branch and department together: the result narrows correctly.
9. Compare against the previous period: absolute and percentage changes are correct.
10. Open a report over a range with no activity: an explicit empty state, not a grid of zeros.
11. Export to Excel in Arabic: headers are Arabic, the sheet is RTL, and an `Export` audit entry exists.

## Done Criteria

- [ ] The aggregation job is idempotent per day and supports backfill.
- [ ] Averages are computed from stored sums over counts under every grouping.
- [ ] Today is included incrementally.
- [ ] Reports read the rollup and return quickly over a year of data.
- [ ] Null (no measurement) is distinguished from zero end to end.
- [ ] Export works in both languages and is audited.
- [ ] Chart conventions are established for the rest of the feature.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
