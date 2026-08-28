# Story 07 — Departments, teams and queue scoping (Story: CS-1203-multi-department)

## Prerequisites

- CS-1001 must be complete: team membership references users.
- The `Ticket` entity is created in CS-201. If that story has not landed, implement the department columns there against the rules defined here rather than duplicating them.

## Story Goal

Support work is organised into departments and teams. Tickets carry a department, agents see their
own department's queue by default, and transferring a ticket between departments is recorded without
resetting the SLA clock.

Teams also carry the rotation order and per-member capacity that automatic assignment (CS-502) depends on,
so this story is a hard prerequisite for that one.

## Context — Read These Files First

1. `.squad/stories/platform/CS-1203-multi-department/intake.md`.
2. [backend/src/CustomerSupport.Domain/Organization/Department.cs](backend/src/CustomerSupport.Domain/Organization/Department.cs), [Team.cs](backend/src/CustomerSupport.Domain/Organization/Team.cs), [TeamMember.cs](backend/src/CustomerSupport.Domain/Organization/TeamMember.cs) — note `RoundRobinCursor`, `MaxConcurrentTickets` and `RotationOrder`.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs) — four departments are already seeded.
4. [backend/src/CustomerSupport.Domain/Tickets/Ticket.cs](backend/src/CustomerSupport.Domain/Tickets/Ticket.cs) — `DepartmentId`, `AssignedTeamId`, `AssignedAgentId` already exist.
5. [backend/src/CustomerSupport.Application/Common/Security/Permissions.cs](backend/src/CustomerSupport.Application/Common/Security/Permissions.cs) — `Tickets.ViewAll` is the permission that lifts department scoping.

## Product rules (from story)

- **Department default order:** category default, then channel account default, then the system default department. Never leave it null.
- **Queue scoping:** without `tickets.view.all`, an agent sees tickets in their own department or assigned to them personally. With it, they see everything in their accessible branches.
- **Transfer clears the assignee** (the new department picks its own owner) and **does not restart the SLA clock**. Internal routing is not the customer's problem, and restarting hides breaches.
- **Every transfer appends a `DepartmentChanged` ticket event** carrying the old and new display names, captured at write time so history survives a later rename.
- **A department with open tickets or active teams cannot be deactivated.**
- **Names are bilingual** (`LocalizedText`), like every other lookup.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/departments` | `admin.departments.manage` or `tickets.view` | Active departments for pickers. |
| POST/PUT | `/api/departments` , `/api/departments/{id}` | `admin.departments.manage` | |
| POST | `/api/departments/{id}/deactivate` | `admin.departments.manage` | Refused when in use. |
| GET | `/api/teams` | `admin.teams.manage` | Filterable by department. |
| POST/PUT | `/api/teams`, `/api/teams/{id}` | `admin.teams.manage` | |
| PUT | `/api/teams/{id}/members` | `admin.teams.manage` | Replaces membership, capacities and rotation order. |
| POST | `/api/tickets/{id}/transfer` | `tickets.assign` | Body: `{ departmentId, teamId?, reason? }`. |

## Backend Tasks

### 1 — Department and team slices

**File:** `backend/src/CustomerSupport.Application/Organization/`

Standard CRUD following the Customers slice. `DeactivateDepartmentCommand` checks for open tickets and active teams first:

```csharp
var openTickets = await db.Tickets.CountAsync(
    t => t.DepartmentId == request.Id && !t.Status.IsTerminal, ct);
if (openTickets > 0)
{
    throw new ConflictException(
        $"Cannot deactivate this department while {openTickets} open ticket(s) remain. Transfer them first.");
}
```

`UpdateTeamMembersCommand` replaces the membership set in one transaction, assigning `RotationOrder` from the submitted order so round-robin is deterministic across restarts.

### 2 — Department scoping on ticket queries

**File:** `backend/src/CustomerSupport.Application/Common/Interfaces/QueryableExtensions.cs`

Add a companion to `WhereBranchAccessible`:

```csharp
/// <summary>
/// Restricts a ticket query to what the caller may see: everything when they hold
/// tickets.view.all, otherwise their own department plus anything assigned to them.
/// </summary>
public static IQueryable<Ticket> WhereTicketVisible(this IQueryable<Ticket> query, ICurrentUser user)
{
    if (user.HasPermission(Permissions.Tickets.ViewAll)) return query;

    var departments = user.DepartmentIds;
    var userId = user.UserId;

    return query.Where(t =>
        t.AssignedAgentId == userId ||
        (t.DepartmentId != null && departments.Contains(t.DepartmentId.Value)));
}
```

Every ticket list, count and report query must call this. Add it to the ticket query the moment CS-201 creates it.

### 3 — Transfer command

**File:** `backend/src/CustomerSupport.Application/Tickets/Commands/TransferTicketCommand.cs`

Load the ticket and both department names **before** mutating, so the event can record readable old and new values:

```csharp
var oldName = ticket.DepartmentId is null ? null : departmentNames[ticket.DepartmentId.Value];

ticket.DepartmentId = request.DepartmentId;
ticket.AssignedTeamId = request.TeamId;
ticket.AssignedAgentId = null;   // the receiving department picks its own owner
ticket.AssignedAt = null;

events.Record(ticket.Id, TicketEventType.DepartmentChanged,
    field: nameof(Ticket.DepartmentId),
    oldValue: oldId?.ToString(), newValue: request.DepartmentId.ToString(),
    oldDisplay: oldName, newDisplay: newName,
    metadataJson: request.Reason is null ? null : JsonSerializer.Serialize(new { request.Reason }));
```

**Do not touch `TicketSlaClock`.** The clock continues across a transfer by design.

## Frontend Tasks

### 4 — Departments and teams admin

**File:** `frontend/src/app/features/admin/organization/`

Two list pages plus forms, following the customers pattern. The team form embeds a member editor: an agent picker, and per member a capacity number input and a drag handle setting rotation order. Persist the whole membership set on save.

### 5 — Department filter and transfer dialog

Add a department filter to the ticket list, populated from `/api/departments`, and hide it entirely for users without `tickets.view.all` — they only ever see one department, so the control would be noise.

The transfer dialog takes a department, an optional team and an optional reason, and warns inline that the current assignee will be cleared.

## Verification Steps

1. Create a department and a team, add two agents with different capacities and rotation orders, and confirm the values persist.
2. Try to deactivate a department that has an open ticket: refused with a clear message.
3. Sign in as an agent without `tickets.view.all`: only their department's tickets and their own assignments are listed.
4. Grant `tickets.view.all` and confirm the full list appears after a token refresh.
5. Transfer a ticket: the assignee clears, a `DepartmentChanged` event appears in history with readable old and new names, and the SLA due dates are unchanged.
6. Rename the old department afterwards and confirm the history entry still shows the original name.
7. Confirm department and team names render in both languages.

## Done Criteria

- [ ] Department and team CRUD with membership, capacity and rotation order.
- [ ] `WhereTicketVisible` exists and is applied to every ticket query.
- [ ] Transfer clears the assignee, records history with display names, and leaves SLA clocks running.
- [ ] Deactivation is blocked while a department is in use.
- [ ] Admin screens are permission-gated and translated.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
