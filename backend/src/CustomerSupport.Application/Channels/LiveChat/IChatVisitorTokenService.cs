namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Issues the scoped token a chat visitor's browser holds instead of a real login. It carries a
/// <c>chat_session</c> claim for exactly one session id and nothing else — no user id, no
/// permissions — signed with the same key as every other JWT so it flows through the one
/// authentication scheme the API already has (see <c>ChatAccess</c> in the Api project for how it
/// is checked against an agent's real permission-bearing token).
/// </summary>
public interface IChatVisitorTokenService
{
    string IssueSessionToken(Guid sessionId);
}
