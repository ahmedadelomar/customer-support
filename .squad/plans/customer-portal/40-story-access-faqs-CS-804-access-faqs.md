# Story 40 — Public help centre and ticket deflection (Story: CS-804-access-faqs)

## Prerequisites

- CS-601 to CS-604 must all be complete.
- CS-801 must be complete for the portal shell.

## Story Goal

A public help centre that answers questions before they become tickets — and measures how often it
does, so the knowledge base can be justified with evidence rather than faith.

## Context — Read These Files First

1. `.squad/stories/customer-portal/CS-804-access-faqs/intake.md`.
2. [backend/src/CustomerSupport.Application/KnowledgeBase/](backend/src/CustomerSupport.Application/KnowledgeBase/) (CS-601–604) — the public endpoints already filter to published public content.
3. [backend/src/CustomerSupport.Domain/KnowledgeBase/KbSearchLog.cs](backend/src/CustomerSupport.Domain/KnowledgeBase/KbSearchLog.cs) — `Source` distinguishes portal searches from agent ones.
4. [frontend/src/app/features/portal/](frontend/src/app/features/portal/) (CS-801) — the shell and public route area.

## Product rules (from story)

- **No sign-in required to read.** The help centre is public; only submitting and tracking need an account.
- **Deflection is measured, not assumed.** Record when a customer opens an article from the ticket form and then does not submit.
- **When an article does not help, offer a ticket** with the article referenced on it — that reference is what tells authors which articles are failing.
- **Suggestions on the ticket form are debounced and unobtrusive.** A form that fights the user while they type is worse than no suggestions.
- **Portal searches are logged with `Source = "Portal"`**, feeding the same gap report as agent searches but distinguishable from them.

## Data model

Add one entity to make deflection measurable:

```csharp
// backend/src/CustomerSupport.Domain/KnowledgeBase/KbDeflectionEvent.cs
public class KbDeflectionEvent : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? VisitorKey { get; set; }

    /// <summary>The draft subject the visitor had typed when the article was suggested.</summary>
    public string? DraftSubject { get; set; }

    /// <summary>Set when the visitor submitted a ticket anyway — the article did not deflect.</summary>
    public Guid? SubmittedTicketId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
```

Index `(ArticleId, OccurredAt)`. A row with a null `SubmittedTicketId` after the session ends counts as a deflection.

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Public help endpoints and deflection tracking

Expose `/api/public/kb/categories`, `/articles`, `/articles/{slug}`, `/search` and `/suggest`, all anonymous, all filtered to published public content by the CS-601 handler rule.

Add `/api/public/kb/home` returning featured articles, the most-viewed of the last 30 days, and categories with their counts — one request for the landing page.

Deflection: `POST /api/public/kb/deflection` records a `KbDeflectionEvent` when a suggested article is opened from the ticket form. If the visitor then submits within the session, `CreatePortalTicketCommand` stamps `SubmittedTicketId` on their recent rows. A nightly job counts rows still unstamped after 24 hours as successful deflections.

Rate-limit the public endpoints per IP — anonymous read endpoints are a scraping target.

## Frontend Tasks

### 2 — Help centre pages

A landing page with a prominent search box, featured articles, popular articles and category cards with counts.

Category and article pages using the shared article renderer from CS-601, with breadcrumbs, related articles and the helpful/not-helpful footer.

Below the feedback footer: "Still need help? Submit a request", carrying the article id so the resulting ticket references it.

### 3 — Inline suggestions on the ticket form

As the visitor types the subject, debounce 400ms and call `/suggest` after three characters, showing up to three suggestions in a panel beside the form — never above it, and never stealing focus.

Opening a suggestion records the deflection event and shows the article in a side panel with the form still filled in behind it, so abandoning is easy and resuming is easier.

If they submit anyway, the ticket references the suggested articles, which is exactly the signal authors need.

## Verification Steps

1. Visit the help centre signed out: categories and articles are browsable and searchable.
2. Confirm internal and draft articles are absent from every public response.
3. Search from the portal: the log records `Source = "Portal"`.
4. Type a subject on the ticket form: suggestions appear after three characters without stealing focus.
5. Open a suggestion: a deflection event is recorded and the form state is preserved behind the panel.
6. Abandon the form after reading the article: after 24 hours the event counts as a deflection.
7. Submit anyway: the event is stamped with the ticket id and does not count as a deflection.
8. Confirm the resulting ticket references the suggested article.
9. Vote not helpful on an article and then submit a ticket from the same page: the ticket references the article.
10. Hammer a public endpoint from one IP: rate limiting engages.
11. Confirm the help centre renders correctly in Arabic with RTL.

## Done Criteria

- [ ] Public help centre is browsable and searchable without sign-in, exposing only published public content.
- [ ] The landing page loads in one request.
- [ ] Suggestions appear on the ticket form, debounced and non-intrusive.
- [ ] Deflection events are recorded and resolved by a nightly job.
- [ ] Tickets reference the articles that failed to answer the question.
- [ ] Public endpoints are rate-limited.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
