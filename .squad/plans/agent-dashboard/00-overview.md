# agent-dashboard — plan overview

Entry point for the **Section 4 — Agent Dashboard** feature. Stories execute in order by their `NN` prefix.

The agent workspace: what to work on, who the customer is, what to follow up, how to answer fast,
and how to pull in a colleague. Every story here is about reducing the number of screens an agent needs open
to do one piece of work.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 24 | [24-story-assigned-tickets-CS-401-assigned-tickets.md](24-story-assigned-tickets-CS-401-assigned-tickets.md) | Agent dashboard and the next-up queue | CS-401-assigned-tickets | 14 |
| 25 | [25-story-customer-information-CS-402-customer-information.md](25-story-customer-information-CS-402-customer-information.md) | Customer panel on the ticket screen | CS-402-customer-information | 24 |
| 26 | [26-story-tasks-reminders-CS-403-tasks-reminders.md](26-story-tasks-reminders-CS-403-tasks-reminders.md) | Agent tasks and timed reminders | CS-403-tasks-reminders | 24 |
| 27 | [27-story-quick-replies-CS-404-quick-replies.md](27-story-quick-replies-CS-404-quick-replies.md) | Quick replies with placeholders and shortcuts | CS-404-quick-replies | 24 |
| 28 | [28-story-team-collaboration-CS-405-team-collaboration.md](28-story-team-collaboration-CS-405-team-collaboration.md) | Mentions, watchers, presence and handover | CS-405-team-collaboration | 27 |

## Dependency notes

- **All five depend on CS-201.** They decorate the ticket screen and the dashboard, both of which that story creates.
- Story 24 (assigned tickets) needs CS-501 for real SLA due times. Until it lands, the urgency ordering falls back to priority and age — implement the ordering so swapping in SLA data is a query change, not a redesign.
- Story 28 (collaboration) reuses the SignalR hub from CS-303 for presence. Do not introduce a second real-time transport.
- Stories 26 (tasks) and 28 (collaboration) both need CS-504 for notification delivery. They can be built against `INotificationDispatcher` before it has real channels behind it.
- Story 27 (quick replies) is a prerequisite for CS-702 (AI suggested replies), which inserts into the same composer and reuses the same placeholder resolution.
