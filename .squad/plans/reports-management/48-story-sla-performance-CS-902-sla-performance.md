# Story 48 — SLA compliance and breach analysis (Story: CS-902-sla-performance)

## Prerequisites

- CS-501 and CS-901 must be complete.
- **Response times must be reported in working hours**, matching the SLA engine. Wall-clock figures would contradict the breach flags.

## Story Goal

Managers see whether commitments are being met, where they are missed, and by how much — in the same
working-hours terms the SLA engine uses, so the report and the breach flags never disagree.

## Context — Read These Files First

1. `.squad/stories/reports-management/CS-902-sla-performance/intake.md`.
2. [backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs](backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs) — the SLA met/breached counts and total-minutes columns.
3. [backend/src/CustomerSupport.Infrastructure/Services/BusinessCalendarCalculator.cs](backend/src/CustomerSupport.Infrastructure/Services/BusinessCalendarCalculator.cs) (CS-501) — the same calculation the report must use.
4. [backend/src/CustomerSupport.Domain/Sla/TicketSlaClock.cs](backend/src/CustomerSupport.Domain/Sla/TicketSlaClock.cs) — `ElapsedMinutes` is already working minutes with pauses excluded.

## Product rules (from story)

- **Elapsed time comes from `TicketSlaClock.ElapsedMinutes`**, which already excludes paused and non-working time. Do not recompute from timestamps.
- **Tickets with no SLA policy are excluded from compliance rates** and reported separately, so they cannot distort the percentage.
- **Show counts beside percentages.** A 100% compliance rate over 2 tickets is not a result.
- **Median alongside average.** A handful of extreme tickets makes the average meaningless on its own.
- **Every figure is drillable** to the tickets behind it, or nobody trusts it.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Compliance and breach queries

Compliance from the rollup:

```csharp
FirstResponseCompliance = total == 0 ? null
    : (double)g.Sum(m => m.FirstResponseMetCount) / total,   // null when nothing was measured
```

where `total = MetCount + BreachedCount` — tickets with no clock contribute to neither and are counted separately as `NoSlaCount`.

Breach analysis groups breached tickets by category, priority, department, agent, channel and hour of arrival, ordered by breach count. Include a `ticketIds` sample or a drill-through link so any row can be opened.

### 2 — Median as a deliberate exception

Medians cannot be derived from daily rollups. Compute them from `TicketSlaClock` over the selected range:

```sql
SELECT DISTINCT
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY c.ElapsedMinutes)
        OVER (PARTITION BY c.TargetType) AS MedianMinutes,
    c.TargetType
FROM TicketSlaClocks c
INNER JOIN Tickets t ON t.Id = c.TicketId
WHERE c.Status IN (2, 3)  -- Met or Breached
  AND t.CreatedAt >= @from AND t.CreatedAt < @to
```

Add a supporting index on `(Status, TicketId)` including `ElapsedMinutes`. Document in a comment that this is the one metric that reads the source tables rather than the rollup, and why — otherwise a future maintainer will "fix" it.

Cap the range for median queries (for example 180 days) and say so in the UI rather than letting a five-year median time out.

## Frontend Tasks

### 3 — SLA report

A header of compliance gauges for first response and resolution, each showing the percentage, the counts behind it, and a target line.

A trend chart over time with the target line overlaid, and a breach-analysis section with tabs per dimension, each row drilling through to the filtered ticket list.

A small "excluded: N tickets with no SLA policy" note beside the compliance figures, linking to those tickets. Hiding them would be the easy way to make the number look better and the honest thing is to show them.

Mark figures derived from fewer than a configured number of tickets as low-confidence.

## Verification Steps

1. Compare reported compliance against a manual count of met and breached clocks for a known day.
2. Confirm response times are in working hours: a ticket created Thursday 16:00 and answered Sunday 09:00 reports about 1 hour, not 65.
3. Confirm a paused period is excluded from elapsed time.
4. Create tickets with no SLA policy: they are excluded from compliance and shown in the separate count.
5. Confirm both average and median are shown and differ where there are outliers.
6. Drill into a breach-analysis row: the filtered ticket list matches the count.
7. Confirm counts are shown beside every percentage.
8. Request a median over a range beyond the cap: a clear message rather than a timeout.
9. Cross-check a compliance figure against the SLA breach flags on the tickets themselves — the two must agree.

## Done Criteria

- [ ] Compliance and breach reporting read the rollup and match the underlying clocks.
- [ ] All durations are working hours, consistent with the SLA engine.
- [ ] No-SLA tickets are excluded and separately reported.
- [ ] Median is computed from source with a documented exception and a range cap.
- [ ] Counts accompany percentages and low-sample figures are marked.
- [ ] Every figure drills through to its tickets.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
