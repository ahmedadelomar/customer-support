# reports-management — plan overview

Entry point for the **Section 9 — Reports & Management** feature. Stories execute in order by their `NN` prefix.

Measurement. Every report in this feature reads the pre-aggregated `TicketDailyMetric` rollup rather
than scanning the ticket table, which is what keeps management screens fast as volume grows. Story 47 builds
that rollup; the rest read it.

The recurring correctness rule here: **never average an average.** Sums and counts are stored separately
precisely so they recombine correctly under any grouping.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 47 | [47-story-ticket-reports-CS-901-ticket-reports.md](47-story-ticket-reports-CS-901-ticket-reports.md) | Daily aggregation and ticket reports | CS-901-ticket-reports | 14 |
| 48 | [48-story-sla-performance-CS-902-sla-performance.md](48-story-sla-performance-CS-902-sla-performance.md) | SLA compliance and breach analysis | CS-902-sla-performance | 47 |
| 49 | [49-story-agent-performance-CS-903-agent-performance.md](49-story-agent-performance-CS-903-agent-performance.md) | Agent performance and workload | CS-903-agent-performance | 48 |
| 50 | [50-story-customer-satisfaction-CS-904-customer-satisfaction.md](50-story-customer-satisfaction-CS-904-customer-satisfaction.md) | Satisfaction reporting and comment review | CS-904-customer-satisfaction | 48 |
| 51 | [51-story-management-dashboards-CS-905-management-dashboards.md](51-story-management-dashboards-CS-905-management-dashboards.md) | Configurable dashboards and scheduled reports | CS-905-management-dashboards | 50 |

## Dependency notes

- **Story 47 must land first.** It builds the aggregation job and the query layer that 48–51 read.
- **Averages are computed from stored sums and counts, never by averaging pre-computed averages.** `TicketDailyMetric` stores `TotalFirstResponseMinutes` alongside the counts, and `CsatScoreSum` alongside `CsatResponseCount`, for exactly this reason.
- Medians cannot be aggregated from daily rollups. Story 48 computes them from the ticket table over the selected range — a deliberate exception to the read-the-rollup rule, with an index to support it.
- Story 50 (satisfaction) depends on CS-805 having landed, since it reads real survey responses.
- Story 51 (dashboards) must call the same report endpoints the report pages use, so each metric has one implementation and cannot drift between a page and a widget.
- **Distinguish "no data" from "zero" everywhere.** A zero that is actually an absence of measurement is the most common way a dashboard misleads.
