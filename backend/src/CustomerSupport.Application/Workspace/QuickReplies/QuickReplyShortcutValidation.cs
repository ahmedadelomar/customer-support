using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>
/// Shortcuts are unique within a scope, per the story's own rule. The filtered database index
/// (Scope, OwnerId, TeamId, Shortcut) enforces uniqueness within one exact owner/team, but cannot
/// express "does this personal shortcut collide with a global one" — that cross-scope ambiguity is
/// checked here, shared by create and update so the two can never disagree about it.
/// </summary>
internal static class QuickReplyShortcutValidation
{
    public static async Task EnsureUnambiguousAsync(
        IAppDbContext db, ICurrentUser currentUser, string? shortcut, Guid? excludingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return;

        var teamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId && m.IsActive)
            .Select(m => m.TeamId)
            .ToListAsync(ct);

        var clashes = await db.QuickReplies.AnyAsync(q =>
            q.Shortcut == shortcut && q.Id != excludingId && q.IsActive &&
            (q.Scope == "Global"
             || (q.Scope == "Team" && q.TeamId != null && teamIds.Contains(q.TeamId.Value))
             || (q.Scope == "Personal" && q.OwnerId == currentUser.UserId)), ct);

        if (clashes)
        {
            throw new ConflictException($"The shortcut '{shortcut}' is already in use in your scope.");
        }
    }
}
