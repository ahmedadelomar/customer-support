using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Categories;

/// <summary>
/// Deactivates or reactivates a category. Unlike a department (CS-1203), a category referenced by
/// open tickets may always be deactivated — it simply disappears from the create picker while
/// continuing to render on the tickets that already carry it; nothing is ever deleted.
/// </summary>
[RequirePermission(Permissions.Tickets.ManageCategories)]
public record SetTicketCategoryActiveCommand(Guid Id, bool IsActive) : IRequest;

public class SetTicketCategoryActiveCommandHandler(IAppDbContext db) : IRequestHandler<SetTicketCategoryActiveCommand>
{
    public async Task Handle(SetTicketCategoryActiveCommand request, CancellationToken cancellationToken)
    {
        var category = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketCategory), request.Id);

        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}
