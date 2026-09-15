using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Notes;

/// <summary>Toggles whether a note is pinned to the top of the customer panel.</summary>
[RequirePermission(Permissions.Customers.ManageNotes)]
public record ToggleNotePinCommand(Guid CustomerId, Guid NoteId) : IRequest<bool>;

public class ToggleNotePinCommandHandler(IAppDbContext db) : IRequestHandler<ToggleNotePinCommand, bool>
{
    public async Task<bool> Handle(ToggleNotePinCommand request, CancellationToken cancellationToken)
    {
        var note = await db.CustomerNotes.FirstOrDefaultAsync(
            n => n.Id == request.NoteId && n.CustomerId == request.CustomerId && !n.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerNote), request.NoteId);

        note.IsPinned = !note.IsPinned;
        await db.SaveChangesAsync(cancellationToken);

        return note.IsPinned;
    }
}
