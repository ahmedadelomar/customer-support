# Customer Support CRM — Project Specification

> **Purpose**: A self-contained, AI-readable specification of this monorepo. Hand this file to any AI model
> or new engineer to give them a complete mental model: what the product is, its stack, architecture, the
> conventions every feature follows, and where each kind of code lives.

---

## 1. What the product is

A bilingual (Arabic / English, RTL-first) **customer support CRM**: customers raise requests through email,
WhatsApp, live chat, SMS, web forms or a self-service portal; agents work them in a shared workspace with
SLA tracking, automatic routing, a knowledge base and AI assistance; managers measure the result.

Twelve functional areas, taken from the source requirements document:

| # | Area | Feature slug |
|---|---|---|
| 1 | Customer Management | `customer-management` |
| 2 | Ticket Management | `ticket-management` |
| 3 | Communication Channels | `communication-channels` |
| 4 | Agent Dashboard | `agent-dashboard` |
| 5 | SLA & Automation | `sla-automation` |
| 6 | Knowledge Base | `knowledge-base` |
| 7 | AI Features | `ai-features` |
| 8 | Customer Portal | `customer-portal` |
| 9 | Reports & Management | `reports-management` |
| 10 | Security & Administration | `security-administration` |
| 11 | Integrations | `integrations` |
| 12 | Platform | `platform` |

There are three audiences and three route areas: **agents** (`/agent`), **administrators** (`/admin`) and
**customers** (`/portal`). All three are served by one Angular application and one API.

---

## 2. Repository layout

```
customer-support/
├── .squad/                 squad-kit workspace: intakes, plans, this spec
│   ├── config.yaml
│   ├── plans/              12 feature folders, 55 story plans, plus 00-index.md
│   └── stories/            12 feature folders, 55 intakes
├── backend/                .NET solution (see backend/README.md)
│   ├── src/
│   │   ├── CustomerSupport.Domain/
│   │   ├── CustomerSupport.Application/
│   │   ├── CustomerSupport.Infrastructure/
│   │   └── CustomerSupport.Api/
│   └── tests/
└── frontend/               Angular application
    ├── public/i18n/        en.json, ar.json
    └── src/app/
        ├── core/           auth, http interceptors, services, models, permissions
        ├── shared/ui/      the reusable component kit
        ├── layout/         application shells
        └── features/       agent/, admin/, portal/, auth/, errors/
```

---

## 3. Technology stack

| Concern | Backend | Frontend |
|---|---|---|
| Platform | .NET 10, ASP.NET Core | Angular 20.3 |
| Language | C# 13, nullable enabled, warnings as errors | TypeScript 5.9, strict |
| Data | EF Core 10, SQL Server | — |
| Identity | ASP.NET Identity + JWT | signal-based `AuthService` |
| Mediation | MediatR with pipeline behaviours | — |
| Validation | FluentValidation | Reactive Forms + server error mapping |
| Change detection | — | **Zoneless** — async UI state must flow through signals |
| Styling | — | Tailwind CSS 3.4 with CSS-variable brand tokens |
| i18n | `AddRequestLocalization`, `ar` default | `@ngx-translate/core` 17, `ar` default |
| Scheduling | Quartz | — |
| API docs | OpenAPI + Scalar at `/scalar/v1` | — |
| Logging | Serilog | — |
| AI | Anthropic `Anthropic` NuGet SDK | — |
| Tests | xUnit, FluentAssertions, NSubstitute | Karma + Jasmine |

The frontend is a **client-rendered SPA**. SSR was removed from the build: every route is behind
authentication or reads per-user state, so there was nothing meaningful to prerender.

---

## 4. Backend architecture

Clean architecture, dependencies pointing inward only:

```
Api  ──▶  Infrastructure  ──▶  Application  ──▶  Domain
```

- **Domain** — entities, value objects, enums. No EF, no ASP.NET, no packages at all.
- **Application** — commands, queries, DTOs, validators, service abstractions, the permission registry.
  Declares `IAppDbContext`, so handlers never reference Infrastructure.
- **Infrastructure** — `AppDbContext`, EF configurations, interceptors, Identity entities, the seeder,
  service implementations, background jobs.
- **Api** — controllers, JWT setup, problem details, DI composition.

### Request pipeline

```
HTTP → Controller (bind + send) → MediatR
     → AuthorizationBehaviour  (enforces [RequirePermission])
     → ValidationBehaviour     (runs FluentValidation)
     → Handler                 (business logic, IAppDbContext)
     → SaveChangesAsync
         ├─ AuditableEntityInterceptor  (stamps audit columns, converts delete → soft delete)
         └─ AuditLogInterceptor         (writes the audit trail, redacting secrets)
```

Authorisation happens **before** validation, so an unauthorised caller learns nothing about which fields
are invalid.

### Persistence conventions

| Convention | Where it lives | Why |
|---|---|---|
| Bilingual text maps to `<Prop>En` / `<Prop>Ar` | `AppDbContext.ApplyLocalizedTextConvention` | Applied by reflection so ~40 entities don't repeat an `OwnsOne` block |
| Soft delete via global query filter | `AppDbContext.ApplySoftDeleteFilters` | No handler can return deleted rows by accident |
| `Remove()` becomes an update | `AuditableEntityInterceptor` | Deleting is never destructive for auditable data |
| Decimal precision 18,4 everywhere | `AppDbContext.ApplyDecimalPrecision` | SQL Server truncates silently without it |
| Unique indexes filtered on `IsDeleted = 0` | Per-entity configurations | A deleted row must not block reusing its code |
| Branch scoping is **opt-in** per query | `QueryableExtensions.WhereBranchAccessible` | A global filter would hide rows from background jobs and cross-branch reports |
| Reference numbers from SQL sequences | `ReferenceNumberGenerator` | Counting rows races and reuses numbers after deletes |

### Reliability patterns

- **Transactional outbox** (`OutboxMessage`) — notifications, webhooks and outbound messages are written in
  the same transaction as the state change and dispatched by a worker. A provider outage delays rather than loses.
- **Ticket event recorder** (`ITicketEventRecorder`) — adds to the caller's unit of work; the caller saves,
  so history and state commit together.
- **Interaction recorder** (`IInteractionRecorder`) — same pattern for the customer timeline.
- **Circuit breakers** on every external call, with a fail-fast path so an outage never blocks a screen.

---

## 5. Frontend architecture

### Layers

- **core/** — singletons: `AuthService`, `LanguageService`, `AppConfigService`, `ToastService`, the three
  HTTP interceptors, `authGuard` / `permissionGuard`, `HasPermissionDirective`, and `permissions.ts`.
- **shared/ui/** — the component kit every feature reuses: `DataTableComponent`, `PageHeaderComponent`,
  `PaginationComponent`, `SearchInputComponent`, `StateCardComponent`, `EmptyStateComponent`.
- **layout/** — `AgentShellPage` (agent and admin) and the portal shell.
- **features/** — one folder per domain, each with its service, models, routes and pages.

### Interceptor order

`languageInterceptor` → `authInterceptor` → `errorInterceptor`. Error handling is last so it observes
failures from the token-refresh retry rather than the original attempt.

### Conventions every feature follows

1. **All list state lives in the URL.** A filtered view is shareable and survives a refresh.
2. **Columns are `computed`** and read `language.current()`, so a language switch re-translates headers and
   action labels without a refetch.
3. **Loading state is a signal.** The app is zoneless; async state that isn't a signal won't re-render.
4. **Services are stateless.** They call `HttpClient` and return typed observables; state lives in components.
5. **Logical CSS properties only** (`ps-*`, `pe-*`, `start-*`, `end-*`). A `pl-4` will not mirror in Arabic.
6. **Every string is a translation key present in both `en.json` and `ar.json`.**
7. **Server validation errors map onto form controls** via the problem-details `errors` map.
8. **Permission checks gate both routes and controls** — `permissionGuard` and `*hasPermission`.

### The reference slice

`frontend/src/app/features/agent/customers/` and `backend/src/CustomerSupport.Application/Customers/` are
implemented end to end and are the pattern every other feature copies. When a plan says "follow the customers
slice", it means those files.

---

## 6. Security model

- **One Identity table** for agents and customer-portal users, separated by `ApplicationUser.UserType` and
  linked to a profile by `CustomerId`.
- **Permissions are declared once** in `Application/Common/Security/Permissions.cs`, seeded into the database
  from that registry, mirrored in `frontend/src/app/core/permissions.ts`, and carried in the JWT as `perm`
  claims — so authorisation needs no database round trip per request.
- **Roles are bundles of permissions**, stored as data. Six system roles ship seeded.
- **Branch scoping** comes from the `branch` and `branches` claims and is applied per query.
- **Out-of-scope reads return 404, not 403** — a 403 confirms the record exists.
- **Portal tokens carry no permission claims** and cannot reach agent endpoints; agent tokens cannot reach
  portal endpoints.
- **The audit trail is append-only**, covers an opt-in list of security, configuration and customer entities,
  and redacts secrets before writing.

---

## 7. Bilingual and multi-tenant model

- Arabic is the default language and RTL the default direction, applied to `<html>` before first paint.
- **Data is bilingual, not just chrome**: categories, statuses, KB articles and notification text all store
  both languages via the `LocalizedText` value object, with fallback to the other language rather than blank.
- **Outbound customer messages use the customer's preferred language**, not the agent's. Internal
  notifications use the recipient's.
- **Branch** is the tenancy unit. `null` means global. An empty accessible-branch set means unrestricted.
- **Department** scopes ticket queues; `tickets.view.all` lifts the restriction.

---

## 8. Where things are

| Looking for | Backend | Frontend |
|---|---|---|
| An entity | `Domain/<Context>/` | `features/<area>/<feature>/*.models.ts` |
| A list query | `Application/<Feature>/Queries/` | `features/.../*-list.page.ts` |
| A command | `Application/<Feature>/Commands/` | `features/.../*-form.page.ts` |
| Permission keys | `Application/Common/Security/Permissions.cs` | `core/permissions.ts` |
| An endpoint | `Api/Controllers/` | `features/.../*.service.ts` |
| DB mapping | `Infrastructure/Persistence/Configurations/` | — |
| A background job | `Infrastructure/Jobs/` | — |
| Seed data | `Infrastructure/Persistence/Seed/DbSeeder.cs` | — |
| Translations | resource files | `public/i18n/{en,ar}.json` |

---

## 9. Current state

**Implemented:** the full domain model (68 entities), `AppDbContext` with its conventions, EF configurations
and indexes, both interceptors, the seeder, the permission registry, the MediatR pipeline, the Customers
vertical slice end to end, the API host with JWT and problem details, and the Angular shell, component kit,
core services and Customers feature. `npm run build` succeeds.

**Not yet done:** the EF migration (the .NET SDK is not installed on this machine — see `backend/README.md`),
and the 54 remaining stories under `.squad/plans/`.

**Start at** `.squad/plans/00-index.md`, then story `01`.
