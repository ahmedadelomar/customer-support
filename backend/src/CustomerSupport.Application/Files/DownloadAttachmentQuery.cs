using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Files.Dtos;
using CustomerSupport.Domain.Files;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Files;

/// <summary>
/// Streams a file back after re-running <see cref="IAttachmentOwnerAuthorizer"/> — the attachment id
/// alone is never sufficient, no matter how unguessable it looks.
/// </summary>
public record DownloadAttachmentQuery(Guid Id) : IRequest<AttachmentContent>;

public class DownloadAttachmentQueryHandler(
    IAppDbContext db, IAttachmentOwnerAuthorizer authorizer, IFileStorage storage)
    : IRequestHandler<DownloadAttachmentQuery, AttachmentContent>
{
    public async Task<AttachmentContent> Handle(DownloadAttachmentQuery request, CancellationToken cancellationToken)
    {
        // Deliberately bypasses the soft-delete filter: "Deletes are soft, and attachments remain
        // retrievable for audit" is a product rule, not only an accident of how note-deletion
        // happens to leave attachment rows untouched. A soft-deleted attachment is hidden from
        // listings but stays downloadable to anyone who could already access its owner.
        var attachment = await db.Attachments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        // 404, not 403 — matches the rest of the codebase's rule that an out-of-scope record does
        // not confirm its own existence to a caller who cannot see it.
        if (attachment is null || !await authorizer.CanAccessAsync(attachment.OwnerType, attachment.OwnerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Attachment), request.Id);
        }

        if (attachment.ScanResult == "infected")
        {
            throw new ConflictException("This file failed a virus scan and cannot be downloaded.");
        }

        var stream = await storage.OpenAsync(attachment.StorageKey, cancellationToken);
        return new AttachmentContent(stream, attachment.FileName, attachment.ContentType);
    }
}
