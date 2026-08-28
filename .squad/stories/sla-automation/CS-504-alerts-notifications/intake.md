# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/sla-automation/CS-504-alerts-notifications/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 5 — SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-504-alerts-notifications`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `sla, notifications`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Alerts and notifications
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a user of the system,
I want to be told about the things that need me, through the channels I choose,
So that I act in time without watching a screen all day.

Scope:
- In-app notifications with an unread badge and a notification centre.
- Fan-out to email, SMS and push according to per-user, per-event preferences.
- Quiet hours, overridden only by critical alerts.
- Real-time delivery in-app rather than on a poll.
- Notification text rendered in the recipient's language.
- Reliable delivery through the transactional outbox.
- A digest option so high-volume events do not become one notification each.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given an event I have opted into occurs,
Then an in-app notification is created,
And it appears in real time without a page refresh,
And the unread badge increases.

Given my preferences for an event include email,
Then an email is also sent, in my preferred language.

Given I have no preference row for an event,
Then the documented system default applies rather than nothing being sent.

Given quiet hours are set and a non-critical notification occurs within them,
Then external delivery is deferred until quiet hours end,
And the in-app notification is still created immediately.

Given a critical notification such as an SLA breach,
Then it is delivered immediately regardless of quiet hours.

Given I open the notification centre,
Then notifications are listed newest first with unread ones distinguished,
And I can mark one or all as read.

Given a notification links to a record,
Then opening it navigates there and marks it read.

Given the dispatching worker fails mid-delivery,
Then the notification is retried from the outbox rather than lost.

Given a burst of similar events,
Then the digest option groups them into one notification instead of many.

Given notification text, it is rendered in the recipient's language at creation, not at display time.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| — | None. |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CS-1001-users-roles
- **Depends on code areas or other stories:**

- `Notification`, `NotificationPreference` and `OutboxMessage` entities — already defined.
- `INotificationDispatcher` — declared, not implemented.
- CS-301 and CS-304 for the email and SMS senders the fan-out uses.
- CS-303 for the SignalR infrastructure real-time delivery reuses.

## Extra notes (optional)

- Almost every other feature depends on this one. Building it against the interface early and implementing the channels here keeps the rest unblocked.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Rendering text at creation rather than at display means a later template change does not silently rewrite history.

## Out of scope

- What this story explicitly does **not** cover:

- Customer-facing notification preferences — the portal has its own settings.
- A marketing notification system.
