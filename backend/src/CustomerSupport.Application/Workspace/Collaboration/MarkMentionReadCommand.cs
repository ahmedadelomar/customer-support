using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>
/// Marks one mention read. No <c>[RequirePermission]</c> — ownership (only the mentioned user may
/// mark it) is the entire access rule, same pattern as CS-403's <c>DismissReminderCommand</c>.
/// </summary>
public record MarkMentionReadCommand(Guid Id) : IRequest;

public class MarkMentionReadCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
    : IRequestHandler<MarkMentionReadCommand>
{
    public async Task Handle(MarkMentionReadCommand request, CancellationToken cancellationToken)
    {
        var mention = await db.TicketMentions.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.TicketMention), request.Id);

        if (mention.MentionedUserId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the mentioned user may mark this mention read.");
        }

        if (mention.ReadAt is null)
        {
            mention.ReadAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
