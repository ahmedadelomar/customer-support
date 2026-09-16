using System.Security.Claims;
using CustomerSupport.Api.Services;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Infrastructure.Services;

namespace CustomerSupport.Api.Chat;

/// <summary>
/// The one access check every session-scoped chat entry point uses — the REST controller's
/// non-hub actions and every <c>ChatHub</c> method alike — so a visitor's scoped token and an
/// agent's real login token are recognised identically wherever a request names a session id.
/// A visitor may act on exactly the one session its token names; an agent may act on any session
/// once they hold <c>channels.livechat.handle</c>. Group membership on the hub is not proof of
/// either — the story requires re-checking on every call, not only at connect.
/// </summary>
public static class ChatAccess
{
    public static bool CanAccess(ClaimsPrincipal? user, Guid sessionId)
    {
        if (user is null)
        {
            return false;
        }

        var sessionClaim = user.FindFirstValue(ChatVisitorTokenService.SessionClaimType);
        if (sessionClaim == sessionId.ToString())
        {
            return true;
        }

        return user.FindAll(CurrentUserService.PermissionClaimType)
            .Any(c => c.Value == Permissions.Channels.HandleLiveChat);
    }

    /// <summary>True only for a real agent token — used where a visitor must never be allowed even with a matching session id (e.g. accept, promote).</summary>
    public static bool IsAgent(ClaimsPrincipal? user) =>
        user?.FindAll(CurrentUserService.PermissionClaimType).Any(c => c.Value == Permissions.Channels.HandleLiveChat) ?? false;
}
