using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>Deletes a saved view. Only its owner may delete it, regardless of who else it is shared with.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record DeleteSavedTicketViewCommand(Guid Id) : IRequest;

public class DeleteSavedTicketViewCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteSavedTicketViewCommand>
{
    public async Task Handle(DeleteSavedTicketViewCommand request, CancellationToken cancellationToken)
    {
        var view = await db.SavedTicketViews
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Tickets.SavedTicketView), request.Id);

        if (view.OwnerId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the owner of a saved view may delete it.");
        }

        db.SavedTicketViews.Remove(view);
        await db.SaveChangesAsync(cancellationToken);
    }
}
