# Story 23 — Configurable public web forms (Story: CS-305-web-forms)

## Prerequisites

- CS-201 (tickets) and CS-101 (customers) must be complete.
- **This is the most exposed endpoint in the product** — anonymous, public and writing to the database. Treat captcha, rate limiting and validation as load-bearing, not optional.

## Story Goal

Administrators define public forms whose fields are data, not code, and each submission becomes a
ticket. The raw payload is stored before ticket creation is attempted, so a downstream failure never loses a
customer's request.

## Context — Read These Files First

1. `.squad/stories/communication-channels/CS-305-web-forms/intake.md`.
2. [backend/src/CustomerSupport.Domain/Channels/WebFormDefinition.cs](backend/src/CustomerSupport.Domain/Channels/WebFormDefinition.cs) — `FieldsJson`, `RequireCaptcha`, `RateLimitPerHour`.
3. [backend/src/CustomerSupport.Domain/Channels/WebFormSubmission.cs](backend/src/CustomerSupport.Domain/Channels/WebFormSubmission.cs) — the class remark explains why the payload is retained even on failure.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — the `(IpAddress, SubmittedAt)` index for rate limiting and the `(Status, SubmittedAt)` index for the retry job.

## Product rules (from story)

- **Store the payload first, then create the ticket.** A failure after storage is retryable; a failure before it loses the request.
- **Validate against the field schema server-side.** The client form is generated from the same schema, but a public endpoint must never trust it.
- **Captcha and rate limiting are per form**, configured on the definition.
- **A submitter email matching an existing customer links to it** rather than creating a duplicate profile.
- **Only portal-visible categories may be selected** by a public form.
- **Field values are untrusted** and are rendered as text in the ticket description, never as HTML.

## Data model

No new entities. The field schema stored in `FieldsJson`:

```json
[
  {
    "key": "orderNumber",
    "type": "text",
    "label": { "en": "Order number", "ar": "رقم الطلب" },
    "placeholder": { "en": "e.g. ORD-1234", "ar": "مثال: ORD-1234" },
    "required": true,
    "maxLength": 50,
    "pattern": "^ORD-\\d{4}$",
    "mapTo": null
  },
  {
    "key": "email",
    "type": "email",
    "label": { "en": "Email", "ar": "البريد الإلكتروني" },
    "required": true,
    "mapTo": "customerEmail"
  },
  {
    "key": "issueType",
    "type": "select",
    "label": { "en": "Issue type", "ar": "نوع المشكلة" },
    "required": true,
    "options": [{ "value": "billing", "label": { "en": "Billing", "ar": "الفواتير" } }],
    "mapTo": "categoryCode"
  }
]
```

`mapTo` values: `customerEmail`, `customerPhone`, `customerName`, `subject`, `description`, `categoryCode`, `priorityCode`. Unmapped fields are appended to the ticket description as a labelled list.

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/public/forms/{key}` | anonymous | The field schema for rendering. |
| POST | `/api/public/forms/{key}/submit` | anonymous | Captcha-gated and rate-limited. |
| GET/POST/PUT | `/api/web-forms` | `channels.webforms.manage` | Definition CRUD. |
| GET | `/api/web-forms/{id}/submissions` | `channels.webforms.manage` | Including failures. |
| POST | `/api/web-forms/submissions/{id}/retry` | `channels.webforms.manage` | Reprocess a failed submission. |

## Backend Tasks

### 1 — Schema-driven validation

**File:** `backend/src/CustomerSupport.Application/Channels/WebForms/WebFormValidator.cs`

Validate the payload against `FieldsJson`: required fields present, type conformance (text, email, phone, number, date, select, multiselect, textarea, checkbox), `maxLength`, `pattern`, and that `select` values are within the declared options.

Return errors keyed by field so the generated form highlights the right control:

```csharp
var errors = new Dictionary<string, string[]>();
foreach (var field in schema)
{
    payload.TryGetValue(field.Key, out var value);
    if (field.Required && string.IsNullOrWhiteSpace(value))
    {
        errors[field.Key] = [localizer["validation.required"]];
        continue;
    }
    ...
}
if (errors.Count > 0) throw new ValidationException(errors);
```

Compile and cache `pattern` regexes with a match timeout — an administrator-supplied pattern is a potential catastrophic-backtracking vector.

### 2 — Submission handling, store-then-create

```csharp
// 1. Persist the raw payload first, so nothing is lost if the rest fails.
var submission = new WebFormSubmission
{
    WebFormDefinitionId = form.Id,
    BranchId = form.BranchId,
    PayloadJson = JsonSerializer.Serialize(payload),
    SubmitterEmail = mapped.Email,
    SubmitterPhone = mapped.Phone,
    SubmitterName = mapped.Name,
    IpAddress = ip,
    UserAgent = userAgent,
    Status = "Received",
    SubmittedAt = clock.UtcNow,
};
db.WebFormSubmissions.Add(submission);
await db.SaveChangesAsync(ct);

// 2. Then attempt the ticket, recording failure rather than throwing it away.
try
{
    var ticketId = await CreateTicketFromSubmission(submission, form, payload, ct);
    submission.TicketId = ticketId;
    submission.Status = "Processed";
    submission.ProcessedAt = clock.UtcNow;
}
catch (Exception ex)
{
    submission.Status = "Failed";
    submission.FailureReason = ex.Message;
    logger.LogError(ex, "Web form submission {Id} failed to create a ticket", submission.Id);
}
await db.SaveChangesAsync(ct);
```

Customer resolution: match the submitter email or phone (normalised) to an existing contact; otherwise create a customer. Unmapped fields render into the description as `**Label:** value` lines, HTML-escaped.

A Quartz job retries `Failed` submissions with backoff, and an admin can retry manually.

### 3 — Rate limiting and captcha

Rate limit per form per IP using the `(IpAddress, SubmittedAt)` index:

```csharp
var since = clock.UtcNow.AddHours(-1);
var recent = await db.WebFormSubmissions
    .CountAsync(s => s.IpAddress == ip && s.WebFormDefinitionId == form.Id && s.SubmittedAt >= since, ct);

if (form.RateLimitPerHour > 0 && recent >= form.RateLimitPerHour)
{
    throw new ConflictException("Too many submissions from this address. Please try again later.");
}
```

Verify the captcha token with the provider before any processing when `RequireCaptcha` is set. Also apply ASP.NET Core rate limiting at the endpoint as a coarse second layer, since the database check itself costs a query.

## Frontend Tasks

### 4 — Schema-driven form renderer

**File:** `frontend/src/app/features/portal/forms/dynamic-form.component.ts`

Build a reactive form from the fetched schema: one control per field with validators derived from `required`, `maxLength` and `pattern`. Render the control by `type`, with labels and placeholders in the active language.

On a 400, map the returned `errors` map onto controls using the same helper as `customer-form.page.ts`.

Show the configured thank-you message on success, with the ticket number so the submitter can track it.

### 5 — Form builder and submissions viewer

**File:** `frontend/src/app/features/admin/web-forms/`

The builder edits the field list as a sortable list of cards, each with key, type, bilingual labels, required, validation and `mapTo`. Provide a live preview rendering the same `DynamicFormComponent` the public page uses, so what the administrator sees is what visitors get.

Show the public URL and a copyable embed snippet.

The submissions viewer lists submissions with status, submitter, timestamp and the linked ticket, with a **Retry** action on failures and the payload viewable as formatted JSON.

## Verification Steps

1. Create a form with required, pattern-validated and select fields; the public page renders it in both languages.
2. Submit valid data: a submission is stored, a ticket is created and linked, and the thank-you message shows the ticket number.
3. Submit with a missing required field: rejected with a per-field error in the submission language.
4. Submit a select value not in the options list by calling the API directly: rejected.
5. Submit with an email matching an existing customer: the ticket links to them, with no duplicate profile.
6. Submit with an unknown email: a customer is created.
7. Force ticket creation to fail (deactivate the default category): the submission is stored as Failed with a reason and nothing is lost.
8. Fix the cause and retry the submission: the ticket is created.
9. Exceed the hourly limit from one IP: further submissions are rejected.
10. Submit without a captcha token when required: rejected.
11. Submit `<script>alert(1)</script>` in a text field: it appears as text in the ticket description.
12. Confirm portal-hidden categories cannot be selected through a public form.

## Done Criteria

- [ ] Form definitions are data; adding a form needs no schema change or deployment.
- [ ] Server-side validation against the schema, with cached regexes carrying a match timeout.
- [ ] Payload is stored before ticket creation, and failures are retryable both automatically and manually.
- [ ] Captcha and per-form per-IP rate limiting are enforced.
- [ ] Customer matching avoids duplicate profiles.
- [ ] Submitted values are escaped in the ticket description.
- [ ] The builder previews with the same renderer the public page uses.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
