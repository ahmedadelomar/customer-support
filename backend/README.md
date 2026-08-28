# Customer Support CRM — Backend (.NET)

Clean-architecture ASP.NET Core solution. `net10.0`, EF Core 10, ASP.NET Identity, MediatR.

**Status: builds and runs.** Verified end to end — sign in, create a customer, list customers.

## Projects

| Project | Depends on | Holds |
|---|---|---|
| `CustomerSupport.Domain` | nothing | Entities, value objects, enums. No EF, no ASP.NET. |
| `CustomerSupport.Application` | Domain | Commands, queries, DTOs, validators, service abstractions, permission registry. |
| `CustomerSupport.Infrastructure` | Application | `AppDbContext`, EF configurations, interceptors, Identity, seeder, service implementations. |
| `CustomerSupport.Api` | Infrastructure | Controllers, JWT auth, problem details, DI composition. |
| `CustomerSupport.UnitTests` | Application + Infrastructure | xUnit tests. |

Dependencies point inward only. `Application` declares `IAppDbContext`, so handlers never reference
Infrastructure. It does reference the EF Core *abstractions* (for `DbSet<T>` and the async LINQ
operators); the SQL Server provider stays in Infrastructure, so the engine remains swappable.

## Prerequisites

The .NET 10 SDK is installed **per-user** at `%LOCALAPPDATA%\Microsoft\dotnet` (a machine-wide install
needs administrator rights). `DOTNET_ROOT` and `PATH` are already set in your user environment, so a
**new** terminal has `dotnet` on the path.

If `dotnet` is not found, open a fresh terminal, or reinstall with:

```powershell
& "$env:TEMP\dotnet-install.ps1" -Channel 10.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
```

## Running

```bash
cd backend

# Secrets — never commit these
dotnet user-secrets --project src/CustomerSupport.Api set "Jwt:Key" "<at least 32 random chars>"

# Bootstrap admin (only used when no agent user exists yet)
export SEED_ADMIN_PASSWORD='DevAdmin!2026'
export SEED_ADMIN_EMAIL='admin@localhost'

dotnet run --project src/CustomerSupport.Api --urls http://localhost:5256
```

On first start the app creates the database, seeds it, and logs
`Created the bootstrap administrator.` Sign in with `admin` / your `SEED_ADMIN_PASSWORD`.
The account is flagged `MustChangePassword` — the forced-change flow is story 01.

- API: <http://localhost:5256>
- API docs (Scalar): <http://localhost:5256/scalar/v1>
- Health: <http://localhost:5256/health>

## Databases

Two providers, chosen by `Database:Provider`:

| | Development (default) | Production |
|---|---|---|
| Provider | `Sqlite` | `SqlServer` |
| Connection | `Data Source=customer-support.dev.db` | a real connection string |
| Schema | `EnsureCreated()` from the model | `Migrate()` from committed migrations |
| Install needed | none | SQL Server |

SQLite exists **only** so a developer can clone and run with no database server. It is not a
deployment target. Two accommodations are made for it, both isolated and provider-guarded:

- **`AppDbContext.ApplySqliteConversions`** — SQLite has no native `DateTimeOffset` or `decimal` and
  refuses to `ORDER BY` either, so both are converted (timestamps to Unix milliseconds, which still
  sort chronologically). SQL Server mapping is untouched.
- **`ReferenceNumberGenerator`** — SQLite has no sequences, so it falls back to a `ReferenceCounter`
  row incremented inside a transaction. SQL Server uses the real sequences.

### Switching to SQL Server

Install SQL Server (or point at an existing instance), then set in `appsettings.Development.json`
— or better, user-secrets:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost\\SQLEXPRESS;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Database": { "Provider": "SqlServer" }
}
```

The `InitialCreate` migration is committed and already includes the two sequences
(`TicketNumbers`, `CustomerCodes`) added by hand in `Up()`/`Down()`.

```bash
dotnet ef database update --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.Api
```

### Adding a migration

```bash
dotnet-ef migrations add <Name> \
  --project src/CustomerSupport.Infrastructure \
  --startup-project src/CustomerSupport.Api \
  --output-dir Persistence/Migrations
```

Migrations are generated against **SQL Server** (the production target). The SQLite dev database is
rebuilt from the model by `EnsureCreated()`, so it needs no migration — delete
`customer-support.dev.db` to reset it.

## Schema notes

- **Bilingual text** — every `LocalizedText` property maps by convention to `<Prop>En` / `<Prop>Ar`.
  The convention lives in `AppDbContext.ApplyLocalizedTextConvention`, so no entity repeats an
  `OwnsOne` block.
- **Soft delete** — `ISoftDeletable` entities get an automatic `WHERE IsDeleted = 0` filter, and
  `Remove()` is converted to an update by `AuditableEntityInterceptor`. Unique indexes on those
  tables are filtered so a deleted row does not block reuse of its code.
- **Branch scoping is opt-in per query** (`WhereBranchAccessible`), because background jobs and
  cross-branch reports need the whole table.
- **Audit trail** — `AuditLogInterceptor` records mutations for an opt-in list of security and
  configuration entities, with secrets redacted.
- **Reporting** reads `TicketDailyMetric`, a pre-aggregated rollup. Sums and counts are stored
  separately so averages recombine correctly across dimensions.

## Conventions

- One command or query per file, handler in the same file as its request.
- Authorisation is declared with `[RequirePermission(Permissions.X.Y)]` on the request and enforced
  by `AuthorizationBehaviour` — not in controllers.
- Permission keys exist once, in `Application/Common/Security/Permissions.cs`. The seeder and the
  Angular `hasPermission` directive both read from that list.
- Controllers only bind, dispatch and shape the response. `CustomersController` is the reference.
- **Avoid group-join + `DefaultIfEmpty()` for counts** — it yields a nullable value EF cannot
  materialise into a non-nullable `int`. Use a correlated subquery, as `GetCustomersQuery` does.
