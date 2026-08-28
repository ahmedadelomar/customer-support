# customer-portal — plan overview

Entry point for the **Section 8 — Customer Portal** feature. Stories execute in order by their `NN` prefix.

The customer-facing half of the product. Its defining constraint is negative: **nothing internal may
ever reach it** — no internal notes, no mentions, no SLA breach state, no agent-only statuses, no other
customer's data. Every story here is built with portal-specific DTOs rather than reused agent ones.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 37 | [37-story-submit-tickets-CS-801-submit-tickets.md](37-story-submit-tickets-CS-801-submit-tickets.md) | Portal accounts, shell and ticket submission | CS-801-submit-tickets | 14 |
| 38 | [38-story-track-requests-CS-802-track-requests.md](38-story-track-requests-CS-802-track-requests.md) | Tracking and replying to requests | CS-802-track-requests | 37 |
| 39 | [39-story-view-history-CS-803-view-history.md](39-story-view-history-CS-803-view-history.md) | Request history, reopening and self-service profile | CS-803-view-history | 38 |
| 40 | [40-story-access-faqs-CS-804-access-faqs.md](40-story-access-faqs-CS-804-access-faqs.md) | Public help centre and ticket deflection | CS-804-access-faqs | 36 |
| 41 | [41-story-submit-feedback-CS-805-submit-feedback.md](41-story-submit-feedback-CS-805-submit-feedback.md) | Satisfaction surveys and feedback | CS-805-submit-feedback | 38 |

## Dependency notes

- **Portal accounts reuse the agent Identity table**, distinguished by `ApplicationUser.UserType` and linked by `CustomerId`. Both columns already exist. Do not build a second identity system.
- **Never reuse an agent DTO on a portal endpoint.** A shared DTO leaks the next field someone adds to it. This is the single most likely way internal data escapes.
- Story 37 must land first: it creates the portal shell, accounts and route area the rest extend.
- Story 40 (help centre) depends on the whole knowledge base feature (33–36) being complete.
- Story 41 (feedback) is queued by CS-204 on resolution and feeds CS-904. Note the obligation in both directions.
- The portal is the same Angular application and the same API, under a separate route area with its own shell and its own guard.
