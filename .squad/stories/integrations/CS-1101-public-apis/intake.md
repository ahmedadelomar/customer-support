# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/integrations/CS-1101-public-apis/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 11 — Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-1101-public-apis`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `integrations, api`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
APIs
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As an external system or partner,
I want a documented, authenticated API,
So that I can create and read tickets and customers programmatically.

Scope:
- A versioned public API surface distinct from the internal one used by the web app.
- Client credentials authentication issuing scoped tokens.
- Scopes limiting what each client may do, drawn from the same permission model.
- Per-client rate limiting.
- OpenAPI documentation with examples.
- Idempotency keys on writes, so a retried request does not create a duplicate.
- Consistent, documented error responses.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given I am an administrator,
When I create an API client,
Then a client id and secret are generated,
And the secret is shown once and never retrievable afterwards.

Given valid client credentials,
When I request a token,
Then I receive an access token carrying only the scopes granted to that client.

Given a token,
When I call an endpoint outside its scopes,
Then I receive 403 naming the missing scope.

Given a client with an IP allow-list,
Then requests from other addresses are rejected.

Given I exceed the client's rate limit,
Then I receive 429 with a Retry-After header.

Given I send a write request with an idempotency key,
And I retry it with the same key,
Then the original result is returned and no duplicate is created.

Given the API documentation,
Then every endpoint is documented with parameters, responses and examples,
And it is generated from the code so it cannot drift.

Given an error,
Then the response is problem-details with a stable machine-readable code, not only a message.

Given a secret is rotated,
Then the old secret stops working immediately and the rotation is audited.

Given API traffic, it is scoped to the client's branch exactly as a user would be.
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

- **Blocked by / related ids:** CS-201-create-track-tickets
- **Depends on code areas or other stories:**

- `ApiClient` entity — already defined, with scopes, IP ranges, rate limit and secret hash.
- CS-1001 for the permission model scopes derive from.
- CS-101 and CS-201 for the resources exposed.

## Extra notes (optional)

- Idempotency keys matter more on a public API than anywhere else: external callers retry aggressively and cannot see whether the first attempt succeeded.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Keep the public API separate from the internal one. Coupling them means every internal refactor becomes a breaking change for partners.

## Out of scope

- What this story explicitly does **not** cover:

- GraphQL.
- A developer portal with self-service registration.
- Webhooks — that is CS-1104.
