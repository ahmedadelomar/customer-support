# Story 15 — Category tree and priority scale administration (Story: CS-202-categories-priorities)

## Prerequisites

- CS-201 must be complete.
- Five categories and four priorities are already seeded; this story makes them manageable.

## Story Goal

Administrators shape how tickets are classified: a nested category tree with per-node defaults, and a
priority scale that SLA targets attach to. Renaming, moving and deactivating never break existing tickets.

## Context — Read These Files First

1. `.squad/stories/ticket-management/CS-202-categories-priorities/intake.md`.
2. [backend/src/CustomerSupport.Domain/Tickets/TicketCategory.cs](backend/src/CustomerSupport.Domain/Tickets/TicketCategory.cs) — note `Path`, `Depth` and the three default columns.
3. [backend/src/CustomerSupport.Domain/Tickets/TicketPriority.cs](backend/src/CustomerSupport.Domain/Tickets/TicketPriority.cs) — `Level` drives ordering and the escalation "raise priority" action.
4. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfigurations.cs) — `TicketLookupConfiguration`, including the index on `Path`.
5. [backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Seed/DbSeeder.cs) — the seeded values and their path format.

## Product rules (from story)

- **`Path` is materialised as `/parent-code/child-code/`** and is what makes subtree queries a single index seek.
- **Moving a node recomputes the whole subtree in one transaction.** A partial move corrupts the tree.
- **Deactivate, never delete**, anything referenced by a ticket. Deactivated values still render on existing tickets.
- **Exactly one default priority**, enforced on write.
- **A priority referenced by an SLA target cannot be deleted.**
- **Category defaults apply only at creation**, never retroactively — changing a category default must not silently re-prioritise existing tickets.
- **Portal-hidden categories** are selectable by agents but not by customers.

## Data model

_No schema changes: this story reads the existing model._

## API contract

_No new endpoints._

## Backend Tasks

### 1 — Category tree commands

**File:** `backend/src/CustomerSupport.Application/Tickets/Categories/`

`CreateTicketCategoryCommand` computes `Path` and `Depth` from the parent.

`MoveTicketCategoryCommand` is the operation to get right:

```csharp
var oldPath = node.Path;
var newParentPath = newParent?.Path ?? "/";
var newPath = $"{newParentPath}{node.Code}/";

// Rewrite the node and every descendant in one statement, inside one transaction.
await db.TicketCategories
    .Where(c => c.Path.StartsWith(oldPath))
    .ExecuteUpdateAsync(s => s.SetProperty(
        c => c.Path,
        c => newPath + c.Path.Substring(oldPath.Length)), ct);

node.ParentId = request.NewParentId;
node.Depth = (newParent?.Depth ?? -1) + 1;
```

Refuse a move that would make a node its own descendant — check that `newParent.Path` does not start with `oldPath`.

Depth is also recomputed for descendants by the same delta; do it in the same statement or a follow-up update within the transaction.

### 2 — Priority commands with the default invariant

`UpdateTicketPriorityCommand`: when `IsDefault` is set, clear it from every other row in the same transaction. When a caller tries to clear the flag from the only default, refuse — the system must always have one.

`DeleteTicketPriorityCommand` checks `SlaTargets` and `Tickets` before deleting and directs the caller to deactivate instead.

## Frontend Tasks

### 3 — Category tree editor

**File:** `frontend/src/app/features/admin/categories/category-tree.page.ts`

A collapsible tree with drag-to-reparent and drag-to-reorder. Each node shows its bilingual name, code, a portal-visible icon and its defaults as small chips.

Confirm before moving a node with children, stating how many descendants will be re-pathed — this is a bulk operation and should not be a surprise.

The node editor sets both names, code, portal visibility, and the three defaults via pickers.

### 4 — Priority scale editor

A simple ordered list, drag to reorder (which rewrites `Level`), with a colour picker validated as hex, a default radio across rows, and an active toggle. Show a usage count per priority so an administrator can see what deactivating would affect.

## Verification Steps

1. Create a nested category three levels deep and confirm `Path` and `Depth` are correct at each level.
2. Move a mid-tree node with children to a new parent: every descendant path is rewritten, and no orphan remains.
3. Attempt to move a node under its own child: refused.
4. Set category defaults and create a ticket in that category: the defaults apply.
5. Change a category default afterwards and confirm existing tickets are unchanged.
6. Deactivate a category used by open tickets: it disappears from the create picker but still renders on those tickets.
7. Mark a category portal-hidden and confirm it is absent from the portal submit form.
8. Set a new default priority and confirm the previous default was cleared.
9. Attempt to clear the only default: refused.
10. Attempt to delete a priority referenced by an SLA target: refused with a message directing to deactivate.
11. Confirm names render in both languages everywhere categories and priorities appear.

## Done Criteria

- [ ] Category CRUD with correct path and depth maintenance, including subtree moves in one transaction.
- [ ] Self-descendant moves are refused.
- [ ] Category defaults apply at creation only.
- [ ] Priority CRUD maintains exactly one default and blocks deletion when referenced.
- [ ] Deactivated lookups remain visible on existing tickets.
- [ ] Both editors are permission-gated and translated.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
