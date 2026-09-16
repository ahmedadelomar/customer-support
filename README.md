# Customer Support CRM

A bilingual (Arabic / English, RTL-first) customer support CRM. Angular front end, .NET back end, planned
with [squad-kit](https://github.com/AzmSquad/squad-kit).

## Repository layout

| Path | What it is |
|---|---|
| [`.squad/`](.squad/) | The plan: 55 story intakes and 55 implementation plans across 12 feature areas |
| [`backend/`](backend/) | .NET 10 clean-architecture solution — see [backend/README.md](backend/README.md) |
| [`frontend/`](frontend/) | Angular 20 application |

## Start here

1. **[`ARCHITECTURE.md`](ARCHITECTURE.md)** — a file-by-file tour: every service, component and class, what
   it does and why. Start here if you are new to the codebase.
2. **[`.squad/PROJECT_SPEC.md`](.squad/PROJECT_SPEC.md)** — the conventions and product spec.
3. **[`.squad/plans/00-index.md`](.squad/plans/00-index.md)** — the execution order, the critical path, and
   the invariants that run across features.
4. **Story `01`** — [users, roles and sign-in](.squad/plans/security-administration/01-story-users-roles-CS-1001-users-roles.md).

## Running it

Both halves build and run. **No database server is required** — development uses a SQLite file.

Open two terminals.

**1 — API** (creates and seeds the database on first run):

```bash
cd backend
export SEED_ADMIN_PASSWORD='DevAdmin!2026'
dotnet run --project src/CustomerSupport.Api --urls http://localhost:5256
```

**2 — Web:**

```bash
cd frontend
npm install
npm start
```

Then open <http://localhost:4300> and sign in with **`admin`** / the `SEED_ADMIN_PASSWORD` you set.

| | |
|---|---|
| Web | <http://localhost:4300> |
| API | <http://localhost:5256> |
| API docs | <http://localhost:5256/scalar/v1> |
| Health | <http://localhost:5256/health> |

Verified working: sign-in issuing a JWT with all 72 permissions, customer create (with generated
`CUS-000001` codes), paged/filtered list, Arabic round-trip, and the 401/409/400 guard rails.

To reset the dev database, delete `backend/src/CustomerSupport.Api/customer-support.dev.db` and
restart. To switch to SQL Server, see [backend/README.md](backend/README.md).

## How the plans are organised

Each of the 12 feature areas has:

- `.squad/stories/<feature>/<story-id>/intake.md` — the requirement: job story, acceptance criteria in
  Given/When/Then, dependencies, and explicit out-of-scope.
- `.squad/plans/<feature>/00-overview.md` — sequence and dependency notes for the feature.
- `.squad/plans/<feature>/NN-story-*.md` — the implementation plan: prerequisites, goal, which files to read
  first, product rules, data model, API contract, backend and frontend tasks, verification steps, done criteria.

Work **one story at a time in a fresh, scoped agent session**, attaching only that story's plan file. Each
plan ends by instructing the agent to stop and report rather than continuing to the next.

To regenerate the composed planning prompt for any story:

```bash
squad new-plan .squad/stories/<feature>/<story-id>/intake.md
squad list       # every story and whether it has a plan
squad doctor     # workspace health check
```

## Conventions worth knowing before you write code

- **Arabic is the default language and RTL the default direction.** Use logical CSS properties; `pl-4` will
  not mirror.
- **Every user-visible string exists in both `frontend/public/i18n/en.json` and `ar.json`.**
- **The frontend is zoneless.** Async state must flow through signals to re-render.
- **Permission keys are declared once** in `Permissions.cs` and mirrored in `permissions.ts`.
- **The customers slice is the reference pattern** for every list, form, detail page, query and command.

The full set is in [`.squad/PROJECT_SPEC.md`](.squad/PROJECT_SPEC.md) and summarised as invariants in
[`.squad/plans/00-index.md`](.squad/plans/00-index.md).
