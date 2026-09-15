using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

[RequirePermission(Permissions.Workspace.ManageQuickReplies)]
public record DeleteQuickReplyCommand(Guid Id) : IRequest;

public class DeleteQuickReplyCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteQuickReplyCommand>
{
    public async Task Handle(DeleteQuickReplyCommand request, CancellationToken cancellationToken)
    {
        var quickReply = await db.QuickReplies.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.QuickReply), request.Id);

        if (quickReply.Scope == "Global" && !currentUser.HasPermission(Permissions.Workspace.ManageGlobalQuickReplies))
        {
            throw new ForbiddenException("Deleting a global quick reply requires the global quick-replies permission.");
        }

        if (quickReply.Scope == "Personal" && quickReply.OwnerId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the owner may delete a personal quick reply.");
        }

        db.QuickReplies.Remove(quickReply);
        await db.SaveChangesAsync(cancellationToken);
    }
}
