using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CustomerSupport.Application.Channels.LiveChat;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Mints the scoped token a chat widget holds for one anonymous session — same signing key, issuer
/// and audience as <c>TokenService</c>'s real login tokens (so the one JWT bearer scheme in
/// <c>Program.cs</c> accepts both), but with a single <c>chat_session</c> claim and no user id, no
/// role, no <c>perm</c> claims at all. <c>ChatAccess</c> in the Api project is what actually checks
/// that claim against the session a request or hub call names.
/// </summary>
public class ChatVisitorTokenService(IConfiguration configuration) : IChatVisitorTokenService
{
    /// <summary>Claim type carrying the one session id this token is scoped to.</summary>
    public const string SessionClaimType = "chat_session";

    public string IssueSessionToken(Guid sessionId)
    {
        var hours = configuration.GetValue("Jwt:ChatSessionTokenHours", 12);
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: [new Claim(SessionClaimType, sessionId.ToString())],
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
