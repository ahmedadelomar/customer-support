# Story 50 — Satisfaction reporting and comment review (Story: CS-904-customer-satisfaction)

## Prerequisites

- CS-805 must be complete and have real responses.
- CS-901 must be complete.

## Story Goal

Satisfaction reported honestly: never a score without its response rate, low-score comments surfaced
first, and follow-up status visible so a bad rating can be confirmed as handled.

## Context — Read These Files First

1. `.squad/stories/reports-management/CS-904-customer-satisfaction/intake.md`.
2. [backend/src/CustomerSupport.Domain/Portal/CsatSurvey.cs](backend/src/CustomerSupport.Domain/Portal/CsatSurvey.cs) — `AgentId` is the credited agent at send time; `FollowUpTicketId` links the escalation.
3. [backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs](backend/src/CustomerSupport.Domain/Reporting/TicketDailyMetric.cs) — `CsatScoreSum` and `CsatResponseCount` are stored separately so averages recombine.

## Product rules (from story)

- **Never show a score without its response rate.**
- **The credited agent is the one recorded at send time**, not the current assignee.
- **Low-score comments surface first** — they are the actionable content.
- **Follow-up status is shown**, so low scores can be confirmed as handled.
- **Low-sample scores are flagged**, as in story 49.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Satisfaction queries

Score and response rate together:

```csharp
AverageScore = g.Sum(m => m.CsatResponseCount) == 0 ? null
    : (double)g.Sum(m => m.CsatScoreSum) / g.Sum(m => m.CsatResponseCount),
ResponseCount = g.Sum(m => m.CsatResponseCount),
ResponseRate  = surveysSent == 0 ? null : (double)responded / surveysSent,
```

Both nullable — null means no responses, which is not the same as a score of zero.

Distribution reads `CsatSurvey` directly, grouped by score, since the rollup stores only the sum.

The comments query returns responded surveys with comments, ordered by score ascending then date descending, each with the ticket number, agent, category and follow-up ticket status.

## Frontend Tasks

### 2 — Satisfaction report

A header showing average score, response count, response rate and the distribution as a small bar chart — the four together, never the score alone.

A trend chart with the response count plotted as a secondary series, so a rising score on falling responses is visible rather than flattering.

Breakdown tabs by agent, category, channel and department, each with score, count and rate.

A comments feed with a score filter, low scores first, each showing the comment, ticket link, agent and a follow-up badge (**Follow-up open**, **Follow-up resolved**, or **No follow-up**), the last being the one a manager needs to act on.

## Verification Steps

1. Submit several surveys with known scores: the average and count match.
2. Confirm the response rate is correct against surveys sent.
3. Confirm no view shows a score without its response count.
4. Reassign a ticket after its survey was sent, then respond: the score credits the originally recorded agent.
5. Open the comments feed: low scores appear first with their tickets linked.
6. Confirm a low score with an automatic follow-up shows the follow-up status.
7. Create an agent with 2 responses: their score is flagged low-confidence.
8. View a period with no responses: an explicit empty state, not a zero score.
9. Verify a grouped average by hand against the underlying surveys.

## Done Criteria

- [ ] Scores always appear with response counts and rates.
- [ ] Averages recombine correctly from stored sums and counts.
- [ ] The credited agent is the send-time one.
- [ ] Comments are filterable with low scores first and follow-up status shown.
- [ ] Low-sample scores are flagged and empty periods are explicit.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
