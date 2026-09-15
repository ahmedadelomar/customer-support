using System.Text.RegularExpressions;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>Resolved quick-reply body plus any placeholder tokens that could not be filled in.</summary>
public record RenderResult(string Body, IReadOnlyList<string> UnresolvedTokens);

/// <summary>
/// Resolves <c>{{token}}</c> placeholders in a quick-reply template (Agent Dashboard / Quick
/// replies). Lives in Application, not the UI, specifically so CS-702 (AI suggested replies) can
/// reuse the same token vocabulary and resolution logic rather than growing a second one.
/// </summary>
public interface IPlaceholderResolver
{
    Task<RenderResult> RenderAsync(string template, Guid ticketId, CancellationToken ct = default);
}

public class PlaceholderResolver(IAppDbContext db, ICurrentUser currentUser, IAgentDirectory agents) : IPlaceholderResolver
{
    /// <summary>200ms catastrophic-backtracking guard — the pattern itself is simple, but every regex on
    /// untrusted-ish template text gets a timeout as a matter of course.</summary>
    private static readonly Regex TokenPattern =
        new(@"\{\{\s*([a-zA-Z.]+)\s*\}\}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    public async Task<RenderResult> RenderAsync(string template, Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new NotFoundException(nameof(Domain.Tickets.Ticket), ticketId);

        // Resolved in the TICKET's language, never the agent's own UI language — see the story's
        // own rule: a bilingual team must see the customer get the right-language reply regardless
        // of which agent sends it.
        var lang = ticket.Language;

        var agent = currentUser.UserId is { } agentId ? await agents.GetAsync(agentId, ct) : null;

        var branding = await db.BrandingSettings.AsNoTracking()
            .Where(b => b.BranchId == ticket.BranchId)
            .FirstOrDefaultAsync(ct)
            ?? await db.BrandingSettings.AsNoTracking().Where(b => b.BranchId == null).FirstOrDefaultAsync(ct);

        var unresolved = new List<string>();

        var body = TokenPattern.Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            var value = Resolve(token, ticket, lang, agent, branding);

            if (string.IsNullOrWhiteSpace(value))
            {
                unresolved.Add(token);
                // A visible marker, so the agent cannot send a half-filled template by accident —
                // never a raw token, never silently blank.
                return $"[[{token} — not set]]";
            }

            return value;
        });

        return new RenderResult(body, unresolved);
    }

    private static string? Resolve(
        string token,
        Domain.Tickets.Ticket ticket,
        string lang,
        AgentSnapshot? agent,
        Domain.Organization.BrandingSetting? branding) => token switch
        {
            "customer.displayName" => ticket.Customer.DisplayName.For(lang),
            "customer.firstName" => ticket.Customer.FirstName,
            "customer.code" => ticket.Customer.Code,
            "customer.tier" => ticket.Customer.Tier,
            "ticket.number" => ticket.Number,
            "ticket.subject" => ticket.Subject,
            "ticket.status" => ticket.Status.Name.For(lang),
            "ticket.priority" => ticket.Priority.Name.For(lang),
            "ticket.category" => ticket.Category.Name.For(lang),
            "agent.displayName" => agent?.DisplayName.For(lang),
            "agent.jobTitle" => agent?.JobTitle,
            "org.supportEmail" => branding?.SupportEmail,
            "org.supportPhone" => branding?.SupportPhone,
            _ => null,
        };
}
