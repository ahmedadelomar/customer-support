# Story 11 — Contact details management (Story: CS-102-contact-details)

## Prerequisites

- CS-101 must be complete.
- The Contact details tab already exists on `customer-detail.page.html` and renders grouped contacts read-only. This story adds the mutations.
- Verification codes need a send channel. Until CS-301 and CS-304 land, log the code and return it in development only.

## Story Goal

A customer can have any number of typed contacts, with exactly one primary per type. Normalised values
make inbound message routing resolve to the right profile, and verification plus opt-out are respected by
every outbound channel.

## Context — Read These Files First

1. `.squad/stories/customer-management/CS-102-contact-details/intake.md`.
2. [backend/src/CustomerSupport.Domain/Customers/CustomerContact.cs](backend/src/CustomerSupport.Domain/Customers/CustomerContact.cs) — note `NormalizedValue`, `IsPrimary`, `IsVerified` and `AllowNotifications`.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/CustomerConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/CustomerConfigurations.cs) — the `(Type, NormalizedValue)` index that drives inbound matching, and the `(CustomerId, Type, IsPrimary)` index.
4. [backend/src/CustomerSupport.Application/Customers/Commands/CreateCustomerCommand.cs](backend/src/CustomerSupport.Application/Customers/Commands/CreateCustomerCommand.cs) — the existing `Normalize` helper. **Extract and reuse it**; two normalisation rules would break routing.
5. [frontend/src/app/features/agent/customers/customer-detail.page.ts](frontend/src/app/features/agent/customers/customer-detail.page.ts) — `contactGroups` already groups and orders contacts.

## Product rules (from story)

- **Exactly one primary per type.** Promoting one demotes the previous in the same transaction.
- **Normalisation is shared:** email lower-cased and trimmed; phone reduced to digits with an optional leading plus. Inbound routing matches on this value, so a second implementation would silently misroute.
- **Duplicate across customers is a warning, not a block** — households and companies legitimately share numbers — but the agent must confirm, because it degrades inbound routing.
- **Promoting a primary updates the denormalised `Customer.PrimaryEmail` / `PrimaryPhone`** in the same transaction. These columns exist for fast list search and must not drift.
- **Verification codes are 6 digits, expire in 10 minutes, and allow 5 attempts.**
- **`AllowNotifications = false` blocks automated outbound**, but never blocks an agent replying to a ticket the customer opened.
- **A customer must always retain at least one email or phone contact.**

## Data model

No new entity for contacts. Add one for verification:

```csharp
// backend/src/CustomerSupport.Domain/Customers/ContactVerification.cs
public class ContactVerification : BaseEntity
{
    public Guid CustomerContactId { get; set; }
    /// <summary>SHA-256 of the 6-digit code. Storing it in clear would make the log a credential store.</summary>
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

Index `(CustomerContactId, ExpiresAt)`.

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/customers/{customerId}/contacts` | `customers.view` | |
| POST | `/api/customers/{customerId}/contacts` | `customers.contacts.manage` | Returns 409-style warning payload on cross-customer duplicate unless `confirmDuplicate` is set. |
| PUT | `/api/customers/{customerId}/contacts/{id}` | `customers.contacts.manage` | |
| DELETE | `/api/customers/{customerId}/contacts/{id}` | `customers.contacts.manage` | Refused if it would leave no reachable contact. |
| POST | `/api/customers/{customerId}/contacts/{id}/primary` | `customers.contacts.manage` | Demotes the previous primary of that type. |
| POST | `/api/customers/{customerId}/contacts/{id}/verify/send` | `customers.contacts.manage` | |
| POST | `/api/customers/{customerId}/contacts/{id}/verify/confirm` | `customers.contacts.manage` | Body: `{ code }`. |

## Backend Tasks

### 1 — Extract the shared normaliser

**File:** `backend/src/CustomerSupport.Application/Common/ContactNormalizer.cs`

Move the private `Normalize` from `CreateCustomerCommandHandler` into a shared static class, and make both that handler and the new contact commands call it:

```csharp
public static class ContactNormalizer
{
    /// <summary>Lower-cases emails and reduces phone numbers to digits with an optional leading plus.</summary>
    public static string? Normalize(string? value) { /* moved verbatim */ }
}
```

This is the single most important detail in the story: inbound routing (Section 3) matches on `NormalizedValue`, so if creation and contact-add normalise differently, messages land on the wrong profile or create duplicates.

### 2 — Contact commands

**File:** `backend/src/CustomerSupport.Application/Customers/Contacts/`

`AddCustomerContactCommand`, `UpdateCustomerContactCommand`, `DeleteCustomerContactCommand`, `SetPrimaryContactCommand`.

`SetPrimaryContactCommand` must demote and promote atomically, then sync the denormalised column:

```csharp
await db.CustomerContacts
    .Where(c => c.CustomerId == customerId && c.Type == contact.Type && c.Id != contact.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPrimary, false), ct);

contact.IsPrimary = true;

if (contact.Type == ContactType.Email) customer.PrimaryEmail = contact.Value;
else if (contact.Type is ContactType.Mobile or ContactType.Phone) customer.PrimaryPhone = contact.Value;

await db.SaveChangesAsync(ct);
```

`DeleteCustomerContactCommand` refuses when it would leave the customer with no email and no phone. If the deleted contact was primary and another of the same type exists, promote the oldest remaining one rather than leaving the type with no primary.

### 3 — Verification flow

`SendContactVerificationCommand` generates a 6-digit code, stores its SHA-256 hash with a 10-minute expiry, invalidates any earlier unconfirmed code for that contact, and dispatches through the channel matching the contact type.

`ConfirmContactVerificationCommand` compares hashes, increments `Attempts`, and refuses after 5. On success set `IsVerified` and `VerifiedAt`.

Rate-limit sends to 3 per contact per hour, or the endpoint becomes a way to send someone unlimited SMS.

## Frontend Tasks

### 4 — Contact editor on the detail page

**File:** `frontend/src/app/features/agent/customers/contacts/`

Replace the read-only Contact details tab with an editable panel. Each group gets an **Add** button; each row gets **Edit**, **Set primary** (hidden when already primary), **Verify** (email and mobile only, hidden when verified) and **Delete**.

The add/edit dialog switches its fields on type: address types show address line, city, postal code and country; the rest show a single value field validated for the type.

On a duplicate warning from the API, show a confirm step naming the other customer before resubmitting with `confirmDuplicate: true`.

### 5 — Verification dialog

A small dialog: send the code, then a 6-digit input with a countdown to expiry and a resend button disabled until the rate limit allows it. Show remaining attempts after a failure.

## Verification Steps

1. Add a second email and set it primary: the first is demoted and `Customer.PrimaryEmail` matches the new one.
2. Add a contact whose value already belongs to another customer: a warning names that customer, and saving requires confirmation.
3. Add a phone as `+966 50 123 4567` and confirm `NormalizedValue` is `+966501234567`.
4. Create a customer through the create form with the same number in a different format and confirm the duplicate check still catches it — this proves both paths share the normaliser.
5. Delete the only remaining email and phone: refused.
6. Delete a primary contact when another of the same type exists: the remaining one is promoted automatically.
7. Send a verification code, enter it: the contact is marked verified with a timestamp.
8. Enter a wrong code 5 times: further attempts are refused and a new code must be sent.
9. Request 4 codes within an hour: the 4th is rate-limited.
10. Turn off notifications on a contact and confirm automated outbound skips it while a manual agent reply still reaches the customer.

## Done Criteria

- [x] `ContactNormalizer` is shared by every path that writes a contact — verified by creating the
      same phone number in two different formats through both `CreateCustomerCommand` and
      `AddCustomerContactCommand`; the duplicate check caught it either way.
- [x] Exactly one primary per type is enforced transactionally, and the denormalised columns stay in
      sync — verified end to end via the API (add → set-primary → delete-primary-with-successor →
      delete-primary-with-no-successor), each time checking `Customer.PrimaryEmail`/`PrimaryPhone`.
- [x] Cross-customer duplicates warn and require confirmation rather than being silently accepted —
      verified: 409 with `duplicateCustomerId`/`duplicateCustomerName`, then success on resubmit with
      `confirmDuplicate: true`.
- [x] A customer can never be left with no reachable contact — verified: deleting the only email/phone
      contact is refused with 409.
- [x] Verification works with hashed codes, expiry, attempt limits and send rate limiting — verified:
      wrong code decrements attempts and reports the count, the 6th attempt is refused outright, and
      the 4th send within an hour is rate-limited. (Codes are SHA-256 hashed at rest; the 10-minute
      expiry is implemented via `IDateTimeProvider` but not re-verified with a manipulated clock.)
- [ ] `AllowNotifications` is respected by automated outbound and ignored for manual replies — **not
      yet verifiable**. The flag exists, is stored, and is editable from the contact form, but no
      automated outbound sender exists in the codebase yet to honour or ignore it — that arrives with
      CS-301 (email) and CS-304 (SMS). Revisit this checkbox once either of those stories lands.
- [x] The contact panel is fully translated (en/ar key parity verified programmatically) and uses only
      logical CSS properties, matching the rest of the codebase's RTL approach.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
