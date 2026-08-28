# Story 51 — Configurable dashboards and scheduled reports (Story: CS-905-management-dashboards)

## Prerequisites

- CS-901 to CS-904 must be complete — widgets read their endpoints.
- CS-301 must be complete for scheduled email delivery.

## Story Goal

Managers assemble the numbers they care about into one screen, and have them delivered on a schedule.
Widgets call the same endpoints the report pages use, so a metric cannot mean one thing on a page and another
in a widget.

## Context — Read These Files First

1. `.squad/stories/reports-management/CS-905-management-dashboards/intake.md`.
2. [backend/src/CustomerSupport.Domain/Reporting/Dashboard.cs](backend/src/CustomerSupport.Domain/Reporting/Dashboard.cs) and [DashboardWidget.cs](backend/src/CustomerSupport.Domain/Reporting/DashboardWidget.cs) — the 12-column grid model.
3. [backend/src/CustomerSupport.Domain/Reporting/ScheduledReport.cs](backend/src/CustomerSupport.Domain/Reporting/ScheduledReport.cs) — cron, timezone, recipients, format, and `LastRunError`.
4. [backend/src/CustomerSupport.Application/Reporting/Queries/](backend/src/CustomerSupport.Application/Reporting/Queries/) (CS-901–904) — the endpoints widgets must reuse.

## Product rules (from story)

- **Widgets call the existing report endpoints.** A widget with its own query is how a dashboard and a report start disagreeing.
- **Role-scoped dashboards are editable only with the manage permission.**
- **Auto-refresh must not disturb the user** — no layout shift, no lost drag.
- **A widget with no data says so.** Never a zero that looks like a measurement.
- **Scheduled report failures are recorded and visible**, not silent.
- **Widget data respects the viewer's permissions**, not the dashboard author's. A shared dashboard must not leak figures its viewer cannot otherwise see.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Dashboard CRUD and widget data proxy

Standard CRUD for dashboards and widgets. Layout saves as a batch update of positions in one request, since a drag moves several widgets.

`GET /api/dashboards/{id}/widgets/{widgetId}/data` resolves the widget's `MetricKey` or `ReportDefinitionId` to the corresponding report query and dispatches it **as the current user**:

```csharp
// Dispatch through MediatR so the AuthorizationBehaviour applies the VIEWER's permissions,
// not the dashboard author's. Otherwise a shared dashboard leaks restricted figures.
var result = await mediator.Send(BuildQuery(widget, currentUser), ct);
```

A widget whose query the viewer cannot run returns a "not permitted" state the UI renders in place, rather than a 403 that breaks the whole dashboard.

### 2 — Scheduled report runner

A Quartz job polling `(IsActive, NextRunAt)`. For each due schedule: run the report as a system principal scoped to the schedule's branch, render to the chosen format in the chosen language, email it, then set `LastRunAt` and compute the next `NextRunAt` from the cron expression in the schedule's time zone.

On failure, record `LastRunError` and still advance `NextRunAt` — a permanently failing schedule must not block the queue — and notify the schedule owner after three consecutive failures.

## Frontend Tasks

### 3 — Dashboard grid and widgets

A 12-column responsive grid with drag-to-move and resize handles in edit mode, collapsing to a single column below `md` in configured order.

Widget types: stat (large number with change indicator), line, bar, pie, table, gauge and list. Follow the chart conventions established in story 47 so every widget looks like part of one system.

Each widget loads independently, showing its own skeleton, so one slow query does not block the dashboard.

Auto-refresh updates data in place. **Suspend refresh entirely while in edit mode** — refreshing under a drag is how layouts get lost.

### 4 — Widget configuration and scheduled reports

A widget editor with metric selection, visualisation type, date-range preset (relative, such as "last 30 days", so a saved widget stays current), filters and a title in both languages, with a live preview.

A scheduled reports screen listing schedules with their next run, last run and last error, offering **Run now** for testing. Cron entry uses a friendly builder (daily, weekly on a day, monthly on a date, or a raw expression) with the next three run times shown, since raw cron is easy to get subtly wrong.

## Verification Steps

1. Create a dashboard, add four widget types, and confirm each renders real data matching its report page.
2. Drag a widget: the layout persists after reload.
3. Confirm auto-refresh is suspended while dragging.
4. Confirm a slow widget does not block the others.
5. Create a role-scoped dashboard: everyone with the role sees it, and only manage-permission holders can edit it.
6. View a shared dashboard as a user lacking `reports.satisfaction.view`: that widget shows a not-permitted state and the rest of the dashboard still works.
7. Configure a widget with a relative date range, wait a day, and confirm it still shows the right window.
8. View a widget over an empty period: an explicit empty state, not a zero.
9. Create a weekly schedule: the next three run times shown are correct for the chosen time zone.
10. Run it now: the email arrives with the correct format and language.
11. Break the recipient address: the failure is recorded, visible in the list, and the schedule still advances.
12. View the dashboard on a phone: widgets stack in configured order.

## Done Criteria

- [ ] Widgets reuse the report endpoints and are dispatched with the viewer's permissions.
- [ ] A widget the viewer cannot see degrades in place rather than breaking the dashboard.
- [ ] Layout persists, refresh is suspended in edit mode, and widgets load independently.
- [ ] Empty widgets say so rather than showing zero.
- [ ] Scheduled reports run on their cron in the right time zone, and failures are recorded, visible and non-blocking.
- [ ] Dashboards are responsive and translated.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
