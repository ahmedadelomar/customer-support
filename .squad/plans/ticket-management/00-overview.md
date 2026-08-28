# ticket-management — plan overview

Entry point for the **Section 2 — Ticket Management** feature. Stories execute in order by their `NN` prefix.

The central aggregate. Every other feature in the product reads or writes a ticket, so the
invariants set here — history written in the same transaction, display values captured at write time, SLA
clocks resumed rather than restarted — constrain everything that follows.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 14 | [14-story-create-track-tickets-CS-201-create-track-tickets.md](14-story-create-track-tickets-CS-201-create-track-tickets.md) | Ticket creation, list and detail screen | CS-201-create-track-tickets | 10 |
| 15 | [15-story-categories-priorities-CS-202-categories-priorities.md](15-story-categories-priorities-CS-202-categories-priorities.md) | Category tree and priority scale administration | CS-202-categories-priorities | 14 |
| 16 | [16-story-assign-tickets-agents-CS-203-assign-tickets-agents.md](16-story-assign-tickets-agents-CS-203-assign-tickets-agents.md) | Manual assignment, claiming and bulk assign | CS-203-assign-tickets-agents | 14 |
| 17 | [17-story-status-escalation-CS-204-status-escalation.md](17-story-status-escalation-CS-204-status-escalation.md) | Status workflow, resolution, reopening and manual escalation | CS-204-status-escalation | 15 |
| 18 | [18-story-ticket-history-CS-205-ticket-history.md](18-story-ticket-history-CS-205-ticket-history.md) | Ticket history timeline | CS-205-ticket-history | 14 |

## Dependency notes

- **Story 14 must land before any Section 3, 4, 5, 7 or 9 story.** They all read `Ticket`.
- Stories 15–18 can be worked in parallel once 14 is done, but 17 (status and escalation) touches SLA behaviour that CS-501 also owns. Sequence 17 before CS-501, or agree the pause/resume contract in writing first.
- **Every ticket mutation anywhere in the product must call `ITicketEventRecorder`.** Story 18 owns the reader; each story owns its own writes. A mutation without an event is a defect, not an omission.
- Story 14 also owes an `IInteractionRecorder` call (CS-103) on creation and on each message. Do not let the two timelines drift.
- `TicketEventRecorder` and `IReferenceNumberGenerator` already exist in the skeleton. The `TicketNumbers` sequence is created by the CS-1001 migration; ticket creation fails at runtime without it.
