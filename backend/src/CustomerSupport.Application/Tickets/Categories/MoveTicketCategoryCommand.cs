using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Categories;

/// <summary>
/// Reparents a category, rewriting its own and every descendant's <c>Path</c>/<c>Depth</c> in one
/// statement. A partial move would corrupt the materialised-path tree, so this is the one operation
/// in the category slice that touches more than a single row.
/// </summary>
[RequirePermission(Permissions.Tickets.ManageCategories)]
public record MoveTicketCategoryCommand : IRequest
{
    public Guid Id { get; init; }
    /// <summary>Null moves the node to the root.</summary>
    public Guid? NewParentId { get; init; }
}

public class MoveTicketCategoryCommandValidator : AbstractValidator<MoveTicketCategoryCommand>
{
    public MoveTicketCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x).Must(x => x.Id != x.NewParentId)
            .WithMessage("A category cannot be moved under itself.");
    }
}

public class MoveTicketCategoryCommandHandler(IAppDbContext db) : IRequestHandler<MoveTicketCategoryCommand>
{
    public async Task Handle(MoveTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        var node = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketCategory), request.Id);

        TicketCategory? newParent = null;
        if (request.NewParentId is { } newParentId)
        {
            newParent = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == newParentId, cancellationToken)
                ?? throw new NotFoundException(nameof(TicketCategory), newParentId);

            // A move that would make the node its own descendant is refused: the new parent's path
            // must not already sit underneath the node being moved.
            if (newParent.Path.StartsWith(node.Path))
            {
                throw new ConflictException("A category cannot be moved under one of its own descendants.");
            }
        }

        if (node.ParentId == request.NewParentId)
        {
            return;
        }

        var oldPath = node.Path;
        var newParentPath = newParent?.Path ?? "/";
        var newPath = $"{newParentPath}{node.Code}/";
        var depthDelta = ((newParent?.Depth ?? -1) + 1) - node.Depth;

        // Rewrite the node and every descendant's path and depth in one statement.
        await db.TicketCategories
            .Where(c => c.Path.StartsWith(oldPath))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Path, c => newPath + c.Path.Substring(oldPath.Length))
                .SetProperty(c => c.Depth, c => c.Depth + depthDelta), cancellationToken);

        node.ParentId = request.NewParentId;

        await db.SaveChangesAsync(cancellationToken);
    }
}
