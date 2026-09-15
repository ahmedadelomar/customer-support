using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Files;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Files;

/// <summary>Soft-deletes an attachment. The file itself is left on disk — see the remark on <see cref="DownloadAttachmentQuery"/>.</summary>
public record DeleteAttachmentCommand(Guid Id) : IRequest;

public class DeleteAttachmentCommandHandler(IAppDbContext db, IAttachmentOwnerAuthorizer authorizer)
    : IRequestHandler<DeleteAttachmentCommand>
{
    public async Task Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var attachment = await db.Attachments
            .FirstOrDefaultAsync(a => a.Id == request.Id && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Attachment), request.Id);

        if (!await authorizer.CanAccessAsync(attachment.OwnerType, attachment.OwnerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Attachment), request.Id);
        }

        if (attachment.OwnerType == "CustomerNote")
        {
            var note = await db.CustomerNotes.FirstOrDefaultAsync(n => n.Id == attachment.OwnerId, cancellationToken);
            if (note is not null && note.AttachmentCount > 0)
            {
                note.AttachmentCount -= 1;
            }
        }

        // Remove() is converted to a soft delete by AuditableEntityInterceptor — the file on disk
        // is untouched, matching "attachments remain retrievable for audit".
        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
