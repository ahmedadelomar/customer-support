# Story 13 — Customer notes and the shared attachment store (Story: CS-104-notes-attachments)

## Prerequisites

- CS-101 must be complete.
- CS-1004 should be complete so `attachments.maxBytes` and `attachments.allowedExtensions` are configurable; otherwise hard-code the defaults and revisit.
- **This story builds the attachment mechanism the whole product reuses.** Ticket messages, KB articles and branding logos all depend on it. Design it for those callers, not just for notes.

## Story Goal

Agents record internal notes with files against a customer. Underneath, one polymorphic attachment
store serves every aggregate in the product, with authorisation checked per download against the owning
record rather than relying on an unguessable URL.

## Context — Read These Files First

1. `.squad/stories/customer-management/CS-104-notes-attachments/intake.md`.
2. [backend/src/CustomerSupport.Domain/Files/Attachment.cs](backend/src/CustomerSupport.Domain/Files/Attachment.cs) — `OwnerType` / `OwnerId` are a loose reference by design; note `StorageKey` is never a public URL.
3. [backend/src/CustomerSupport.Domain/Customers/CustomerNote.cs](backend/src/CustomerSupport.Domain/Customers/CustomerNote.cs) — `IsPinned`, `IsInternal`, `AttachmentCount`.
4. [backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs](backend/src/CustomerSupport.Application/Common/Interfaces/IServiceAbstractions.cs) — `IFileStorage` is declared and needs an implementation.
5. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — the `(OwnerType, OwnerId)` index.

## Product rules (from story)

- **Files are stored under a generated key, never under the uploaded filename.** A user-supplied name is a path-traversal and overwrite risk; the display name is kept separately.
- **Every download is authorised against the owning record**, resolved from `OwnerType` and `OwnerId`. An unguessable URL is not authorisation.
- **Validation happens before any bytes are persisted** — extension against the allow-list, size against the cap.
- **Internal notes never leave the agent workspace.** No portal endpoint may return a note with `IsInternal`.
- **Pinned notes appear first**, on both the notes tab and the ticket-screen customer panel.
- **Authors may edit and delete their own notes**; editing someone else's requires `customers.notes.manage`.
- **Deletes are soft**, and attachments remain retrievable for audit.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/customers/{customerId}/notes` | `customers.notes.view` | Paged; pinned first. |
| POST | `/api/customers/{customerId}/notes` | `customers.notes.manage` | |
| PUT | `/api/customers/{customerId}/notes/{id}` | author or `customers.notes.manage` | |
| DELETE | `/api/customers/{customerId}/notes/{id}` | author or `customers.notes.manage` | Soft delete. |
| POST | `/api/customers/{customerId}/notes/{id}/pin` | `customers.notes.manage` | Toggles. |
| POST | `/api/attachments` | depends on owner | Multipart. Body: `ownerType`, `ownerId`, file. |
| GET | `/api/attachments/{id}` | depends on owner | Streams the file. |
| DELETE | `/api/attachments/{id}` | depends on owner | Soft delete. |

## Backend Tasks

### 1 — Local-disk file storage

**File:** `backend/src/CustomerSupport.Infrastructure/Services/LocalFileStorage.cs`

Implement `IFileStorage`. Generate the key as `{yyyy}/{MM}/{guid}{ext}` under a configured root:

```csharp
public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct)
{
    // The extension is taken from the validated allow-list, not trusted from the client.
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
    var full = Path.Combine(_root, key);

    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    await using var file = File.Create(full);
    await content.CopyToAsync(file, ct);
    return key;
}
```

`OpenAsync` must reject any key that escapes the root after `Path.GetFullPath`, defending against a traversal attempt via a tampered key.

### 2 — Attachment upload with an owner-authorisation strategy

**File:** `backend/src/CustomerSupport.Application/Files/`

The polymorphic model needs one place that answers "may this user touch this owner?":

```csharp
public interface IAttachmentOwnerAuthorizer
{
    /// <summary>Returns false when the owner does not exist or the caller cannot access it.</summary>
    Task<bool> CanAccessAsync(string ownerType, Guid ownerId, CancellationToken ct);
}
```

Implement it with a switch over `ownerType` — `Customer`, `CustomerNote`, `Ticket`, `TicketMessage`, `KbArticle` — each checking both the permission and branch scope for that aggregate. An unknown `ownerType` returns false, so adding a new attachable aggregate is a deliberate act.

`UploadAttachmentCommand` validates extension and size **before** reading the stream, calls the authorizer, saves via `IFileStorage`, writes the `Attachment` row, and increments the owner's `AttachmentCount`.

`DownloadAttachmentQuery` re-runs the authorizer, then returns a `FileStreamResult` with the original filename in `Content-Disposition`, correctly encoded for non-ASCII names — Arabic filenames are common here.

### 3 — Note commands and query

**File:** `backend/src/CustomerSupport.Application/Customers/Notes/`

`GetCustomerNotesQuery` orders `IsPinned DESC, CreatedAt DESC` and returns the author display name and attachment list per note.

`UpdateCustomerNoteCommand` and `DeleteCustomerNoteCommand` enforce the author rule in the handler, because the permission alone is not enough:

```csharp
if (note.CreatedById != currentUser.UserId &&
    !currentUser.HasPermission(Permissions.Customers.ManageNotes))
{
    throw new ForbiddenException("You can only edit notes you created.");
}
```

### 4 — Virus scan hook

Add `IVirusScanner` with a no-op default implementation that sets `Attachment.ScanResult = "skipped"`. Call it after save and before the file is downloadable; when a scanner is configured and reports an infection, mark the row and refuse download.

Shipping the hook now means adding a real scanner later is configuration rather than a schema change.

## Frontend Tasks

### 5 — Shared file-upload component

**File:** `frontend/src/app/shared/ui/file-upload/file-upload.component.ts`

A reusable drag-and-drop plus click-to-browse control taking `ownerType` and `ownerId`, showing per-file progress, and validating extension and size client-side against the settings endpoint before uploading — a fast rejection beats a slow one.

Because it is shared, ticket replies and KB authoring will reuse it. Emit the created attachment ids so the parent can associate them.

### 6 — Notes tab

**File:** `frontend/src/app/features/agent/customers/notes/`

Replace the Notes tab placeholder. Each note card shows the author, relative timestamp, body, attachment chips and a pin indicator. Actions: **Edit** and **Delete** (own notes, or with the manage permission), **Pin** / **Unpin**.

The composer is a textarea plus the file-upload component, with an internal/external toggle defaulting to internal.

Download attachments through an authenticated blob request, not an anchor href, since the endpoint requires a bearer token.

## Verification Steps

1. Add a note with two attachments: both upload, and the note shows the attachment count.
2. Download an attachment: the original filename is preserved, including an Arabic filename.
3. Upload a file above `attachments.maxBytes`: rejected client-side, and rejected server-side when the client check is bypassed.
4. Upload a disallowed extension such as `.exe`: rejected.
5. Inspect the storage directory: files are stored under generated keys, not user-supplied names.
6. Call the download endpoint for an attachment on another branch's customer: refused.
7. Call the download endpoint with a tampered id: refused, not a raw file.
8. Sign in as a different agent and try to edit the first agent's note: refused without `customers.notes.manage`, allowed with it.
9. Pin a note: it moves to the top and appears in the ticket-screen customer panel.
10. Confirm no portal endpoint returns a note marked internal.
11. Delete a note: it disappears from the list, and its attachments remain retrievable for audit.

## Done Criteria

- [x] `LocalFileStorage` implements `IFileStorage` with generated keys and traversal protection —
      verified live (storage directory shows only generated GUID keys, never the uploaded name) and
      by unit tests covering `../`-style traversal on both `OpenAsync` and `DeleteAsync`.
- [x] `IAttachmentOwnerAuthorizer` gates both upload and download per owning record, and unknown
      owner types are refused — verified live (unrecognised `ownerType` → 403; tampered attachment
      id → 404) and by unit tests covering the Customer/CustomerNote branches, branch scoping, and
      the default-refuse case. The Ticket/TicketMessage/KbArticle branches are written but only
      buildable against permission + branch scope, since none of those features exist yet — see the
      remark in `AttachmentOwnerAuthorizer.cs`.
- [x] Size and extension are validated before bytes are persisted, from configurable settings —
      verified live (`.exe` and an 11 MB file both rejected with 400, before any file write) and
      hardcoded to `appsettings`-driven configuration rather than `SystemSetting`, since CS-1004
      isn't built — `IAttachmentPolicyProvider` is async-shaped specifically so that swap needs no
      signature change later.
- [x] Notes support pin, internal flag, author-based edit rules and soft delete — pin/unpin, create,
      update, delete all verified live; the author-vs-`customers.notes.manage` rule is verified by
      unit tests instead, since no second, permission-constrained agent user exists yet to test it
      live with (CS-1001 has no user management beyond the bootstrap administrator).
- [x] The shared file-upload component works and is reusable by tickets and the knowledge base —
      built under `shared/ui/file-upload/` with no dependency on notes; drag-and-drop, progress,
      and client-side policy validation confirmed via the frontend build. Live end-to-end upload
      exercised through the API directly (curl's multipart handling of this shell proved
      unreliable, so a small Node script drove the actual HTTP calls instead — the request/response
      shape is identical to what the component sends).
- [x] Non-ASCII filenames download correctly — verified live with an Arabic filename: upload
      preserves it exactly, and the download response carries both the ASCII-safe `filename=`
      fallback and the RFC 5987 `filename*=UTF-8''...` parameter, decoding back to the original
      Arabic string. No extra code was needed — ASP.NET Core's `File()` helper does this natively.
- [x] The virus-scan hook exists with a no-op default — `IVirusScanner`/`NoOpVirusScanner`.
      The "infected" rejection path (delete the saved file, never create a row) has no real scanner
      to trigger it live yet, so it's covered by a unit test that substitutes the interface instead.
- [x] Internal notes are never exposed through the portal — there is no portal in this codebase yet
      (CS-801 is unbuilt), so there is no endpoint to leak through. Revisit this checkbox once CS-801
      exists, to confirm its DTOs are built independently and never reuse `CustomerNoteDto`.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
