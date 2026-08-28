# Story 37 — Portal accounts, shell and ticket submission (Story: CS-801-submit-tickets)

## Prerequisites

- CS-1001, CS-101, CS-201 and CS-104 must be complete.
- CS-1205 (branding) should be complete, or the portal ships with the default theme and is re-themed later.
- `ApplicationUser.UserType` and `CustomerId` already exist for this purpose.

## Story Goal

Customers get their own front door: register, verify, sign in, and raise a request. The portal shares
the application and API with the agent workspace but has its own shell, its own guard and its own DTOs.

## Context — Read These Files First

1. `.squad/stories/customer-portal/CS-801-submit-tickets/intake.md`.
2. [backend/src/CustomerSupport.Infrastructure/Identity/ApplicationUser.cs](backend/src/CustomerSupport.Infrastructure/Identity/ApplicationUser.cs) — `UserType` and `CustomerId`; read the class remark on why one table serves both audiences.
3. [backend/src/CustomerSupport.Api/Services/CurrentUserService.cs](backend/src/CustomerSupport.Api/Services/CurrentUserService.cs) — `CustomerId` comes from the `customer_id` claim, already implemented.
4. [backend/src/CustomerSupport.Domain/Tickets/TicketCategory.cs](backend/src/CustomerSupport.Domain/Tickets/TicketCategory.cs) — `IsVisibleInPortal` gates the category picker.
5. [frontend/src/app/layout/agent-shell/agent-shell.page.ts](frontend/src/app/layout/agent-shell/agent-shell.page.ts) — the shell pattern to adapt, much simplified.

## Product rules (from story)

- **A portal account links to an existing customer profile when the email matches.** Creating a duplicate profile splits the history, which is the problem the whole customer feature exists to avoid.
- **Password reset responses are identical whether or not the account exists.** Otherwise the endpoint enumerates customers.
- **Email verification is required before sign-in** for portal accounts, unlike agent accounts which an administrator vouches for.
- **Only `IsVisibleInPortal` categories appear** in the submission form, enforced server-side.
- **A blocked customer cannot submit**, and is told to contact support another way rather than being left guessing.
- **Portal tickets use `ChannelKey.Portal`.**

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/api/portal/auth/register` | anonymous | Rate-limited. Always returns success shape. |
| POST | `/api/portal/auth/verify-email` | anonymous | Token from the verification email. |
| POST | `/api/portal/auth/login` | anonymous | Refuses unverified accounts. |
| POST | `/api/portal/auth/forgot-password` , `/reset-password` | anonymous | Enumeration-safe. |
| GET | `/api/portal/categories` | portal user | Portal-visible categories only. |
| POST | `/api/portal/tickets` | portal user | Creates on the Portal channel. |
| GET | `/api/portal/me` | portal user | Profile summary. |

## Backend Tasks

### 1 — Portal registration with profile linking

**File:** `backend/src/CustomerSupport.Application/Portal/Auth/RegisterPortalUserCommand.cs`

```csharp
var normalized = ContactNormalizer.Normalize(request.Email)!;

// Link to an existing profile rather than duplicating it — a second profile splits the history.
var existingContact = await db.CustomerContacts
    .Where(c => c.NormalizedValue == normalized && c.Type == ContactType.Email && !c.IsDeleted)
    .Select(c => new { c.CustomerId })
    .FirstOrDefaultAsync(ct);

Guid customerId;
if (existingContact is not null)
{
    customerId = existingContact.CustomerId;

    // An account may already exist for this profile. Say nothing that reveals it.
    if (await db.Users.AnyAsync(u => u.CustomerId == customerId && u.UserType == UserType.Customer, ct))
    {
        await mail.SendAccountAlreadyExistsAsync(request.Email, ct);
        return;   // same response as success
    }
}
else
{
    customerId = await mediator.Send(new CreateCustomerCommand { ... }, ct);
}

var user = new ApplicationUser
{
    UserName = request.Email, Email = request.Email,
    UserType = UserType.Customer, CustomerId = customerId,
    PreferredLanguage = request.PreferredLanguage, EmailConfirmed = false, IsActive = true,
};
await userManager.CreateAsync(user, request.Password);
await SendVerificationEmail(user, ct);
```

The endpoint returns the same shape in every branch, so registration cannot be used to discover which email addresses are customers.

Apply the same rule to forgot-password: always report that instructions were sent.

### 2 — Portal token, guard and submission

Portal tokens carry `customer_id` and `UserType = Customer`, but **no `perm` claims** — portal access is not permission-based, it is identity-based. Add a `[PortalOnly]` authorisation policy requiring the customer claim, and apply it to every `/api/portal/*` endpoint.

Equally, agent endpoints must reject portal tokens. Add the inverse check to `ApiControllerBase`'s policy, or a portal user could call agent endpoints simply by holding a valid token.

`CreatePortalTicketCommand` sets `CustomerId` from the claim — **never from the request body** — validates the category is portal-visible, refuses when the customer is blocked, and delegates to `CreateTicketCommand` with `Channel = Portal`.

## Frontend Tasks

### 3 — Portal shell and routes

**File:** `frontend/src/app/layout/portal-shell/`, `frontend/src/app/features/portal/`

A deliberately simpler shell than the agent one: branded header with logo and language toggle, a slim nav (Home, My requests, Help centre), and a footer with the support contact details from branding.

Add a `/portal` route area with a `portalGuard` that requires an authenticated user carrying a customer id, redirecting agents to `/agent` rather than showing them an empty portal.

Public routes (`/portal/help`) sit outside the guard.

### 4 — Registration, verification and submission

Registration with email, password, name and language, showing a live password-policy checklist rather than failing after submit.

After registering, a "check your email" screen — never a signed-in state, since verification is required.

The submission form: category (portal-visible only, as a simple grouped select rather than a tree picker), subject, description and attachments via the shared upload component. On success, a confirmation showing the ticket number with a link to track it.

CS-804 will add article suggestions beside the subject field; leave room in the layout for them.

## Verification Steps

1. Register with an email matching an existing customer: the account links to that profile and no duplicate is created.
2. Register with an unknown email: a customer profile is created and linked.
3. Register with an email that already has a portal account: the response is identical to a fresh registration, and a "you already have an account" email is sent instead.
4. Try to sign in before verifying: refused with a clear message.
5. Verify, then sign in: successful, landing on the portal home.
6. Request a password reset for a non-existent address: the response matches the existing-address case exactly.
7. Submit a ticket: it is created on the Portal channel against the right customer.
8. Confirm only portal-visible categories are offered, and that posting a hidden category id directly is refused.
9. Block the customer and try to submit: refused with guidance.
10. Post a ticket with another customer id in the body: the ticket is still created against the signed-in customer.
11. Call an agent endpoint with a portal token: refused.
12. Confirm the portal renders with branch branding in both languages and correct direction.

## Done Criteria

- [ ] Portal accounts link to existing profiles and never duplicate them.
- [ ] Registration and password reset are enumeration-safe.
- [ ] Email verification gates sign-in.
- [ ] Portal tokens carry no permission claims and cannot reach agent endpoints, and vice versa.
- [ ] Customer id comes from the claim, never the request body.
- [ ] Only portal-visible categories are selectable, enforced server-side.
- [ ] The portal shell is branded, bilingual and separate from the agent shell.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
