# security-administration — plan overview

Entry point for the **Section 10 — Security & Administration** feature. Stories execute in order by their `NN` prefix.

The security foundation for the whole product. Nothing else can be built safely until identities,
roles and permissions exist, which is why this feature holds NN 01–04 even though it is section 10 in the
source document. Stories 01 and 02 must land before any other feature story is started.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-users-roles-CS-1001-users-roles.md](01-story-users-roles-CS-1001-users-roles.md) | Users, roles and sign-in | CS-1001-users-roles | — |
| 02 | [02-story-permissions-CS-1002-permissions.md](02-story-permissions-CS-1002-permissions.md) | Permission catalogue and role editor | CS-1002-permissions | 01 |
| 03 | [03-story-audit-logs-CS-1003-audit-logs.md](03-story-audit-logs-CS-1003-audit-logs.md) | Audit log viewer, auth events and retention | CS-1003-audit-logs | 01 |
| 04 | [04-story-system-configuration-CS-1004-system-configuration.md](04-story-system-configuration-CS-1004-system-configuration.md) | System configuration settings | CS-1004-system-configuration | 01 |

## Dependency notes

- **Strict sequence.** 01 creates identities, 02 makes permissions editable, 03 records what people did, 04 makes behaviour configurable. Each depends on the one before it.
- **Every other feature depends on 01 and 02.** Their route guards and `RequirePermission` attributes assume the permission model exists.
- The `Permission`, `RolePermission`, `AuditLog` and `SystemSetting` entities and the `AuditLogInterceptor` already exist in the skeleton. These stories add the APIs, screens and background jobs around them.
- The permission registry exists twice: `Permissions.cs` on the server and `permissions.ts` on the client. Story 02 adds the test that fails the build when they drift.
- No database migration is committed yet. Story 01 creates `InitialCreate` for the whole schema, including the two sequences described in `backend/README.md`. Later stories add migrations only for what they change.
