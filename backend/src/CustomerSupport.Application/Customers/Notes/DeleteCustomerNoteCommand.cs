using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Notes;

/// <summary>Soft-deletes a note. See the remark on <see cref="UpdateCustomerNoteCommand"/> for why there is no top-level permission attribute.</summary>
public record DeleteCustomerNoteCommand(Guid CustomerId, Guid NoteId) : IRequest;

public class DeleteCustomerNoteCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteCustomerNoteCommand>
{
    public async Task Handle(DeleteCustomerNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await db.CustomerNotes.FirstOrDefaultAsync(
            n => n.Id == request.NoteId && n.CustomerId == request.CustomerId && !n.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerNote), request.NoteId);

        if (note.CreatedById != currentUser.UserId && !currentUser.HasPermission(Permissions.Customers.ManageNotes))
        {
            throw new ForbiddenException("You can only delete notes you created.");
        }

        // Remove() is converted to a soft delete by AuditableEntityInterceptor. Its attachments
        // are untouched — see the remark on DownloadAttachmentQuery — so they stay retrievable
        // even though the note that referenced them no longer appears in the notes list.
        db.CustomerNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
    }
}
