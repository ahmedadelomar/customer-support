using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Workspace;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>
/// Quick replies visible to the caller: their own personal replies, their teams' replies, and every
/// global reply — ordered by usage descending, so the most-used surface first in the picker.
/// </summary>
[RequirePermission(Permissions.Tickets.Reply)]
public record GetQuickRepliesQuery : IRequest<IReadOnlyList<QuickReplyDto>>
{
    public Guid? CategoryId { get; init; }
    public Guid? ChannelId { get; init; }
    /// <summary>Narrows to one scope — used by the management screen's scope filter; the picker leaves this unset.</summary>
    public string? Scope { get; init; }
    /// <summary>Include inactive rows — the management screen only, never the composer picker.</summary>
    public bool IncludeInactive { get; init; }
}

public class GetQuickRepliesQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetQuickRepliesQuery, IReadOnlyList<QuickReplyDto>>
{
    public async Task<IReadOnlyList<QuickReplyDto>> Handle(GetQuickRepliesQuery request, CancellationToken cancellationToken)
    {
        var query = await QuickReplyVisibility.ApplyAsync(
            db.QuickReplies.AsNoTracking(), db, currentUser, cancellationToken);

        if (!request.IncludeInactive) query = query.Where(q => q.IsActive);
        if (request.CategoryId is { } categoryId) query = query.Where(q => q.CategoryId == categoryId);
        if (request.ChannelId is { } channelId) query = query.Where(q => q.ChannelId == channelId);
        if (!string.IsNullOrWhiteSpace(request.Scope)) query = query.Where(q => q.Scope == request.Scope);

        var canManageGlobal = currentUser.HasPermission(Permissions.Workspace.ManageGlobalQuickReplies);
        var canManage = currentUser.HasPermission(Permissions.Workspace.ManageQuickReplies);

        var rows = await query
            .OrderByDescending(q => q.UsageCount)
            .Select(q => new
            {
                q.Id,
                q.Scope,
                q.OwnerId,
                q.TeamId,
                q.Shortcut,
                TitleEn = q.Title.En,
                TitleAr = q.Title.Ar,
                BodyEn = q.Body.En,
                BodyAr = q.Body.Ar,
                q.CategoryId,
                q.ChannelId,
                q.UsageCount,
                q.IsActive,
            })
            .ToListAsync(cancellationToken);

        // TeamId/CategoryId/ChannelId are loose references on QuickReply (no FK navigation, same
        // reasoning as AgentTask's ticket/customer/priority links), so display names are resolved
        // as batched lookups rather than an EF join.
        var teamIds = rows.Where(r => r.TeamId is not null).Select(r => r.TeamId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count == 0
            ? new Dictionary<Guid, (string En, string Ar)>()
            : await db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name.En, t.Name.Ar })
                .ToDictionaryAsync(t => t.Id, t => (t.En, t.Ar), cancellationToken);

        var categoryIds = rows.Where(r => r.CategoryId is not null).Select(r => r.CategoryId!.Value).Distinct().ToList();
        var categoryNames = categoryIds.Count == 0
            ? new Dictionary<Guid, (string En, string Ar)>()
            : await db.TicketCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name.En, c.Name.Ar })
                .ToDictionaryAsync(c => c.Id, c => (c.En, c.Ar), cancellationToken);

        var channelIds = rows.Where(r => r.ChannelId is not null).Select(r => r.ChannelId!.Value).Distinct().ToList();
        var channelNames = channelIds.Count == 0
            ? new Dictionary<Guid, (string En, string Ar)>()
            : await db.Channels.AsNoTracking().Where(c => channelIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name.En, c.Name.Ar })
                .ToDictionaryAsync(c => c.Id, c => (c.En, c.Ar), cancellationToken);

        return rows.Select(q => new QuickReplyDto
        {
            Id = q.Id,
            Scope = q.Scope,
            OwnerId = q.OwnerId,
            TeamId = q.TeamId,
            TeamNameEn = q.TeamId is { } tId && teamNames.TryGetValue(tId, out var tn) ? tn.En : null,
            TeamNameAr = q.TeamId is { } tId2 && teamNames.TryGetValue(tId2, out var tn2) ? tn2.Ar : null,
            Shortcut = q.Shortcut,
            TitleEn = q.TitleEn,
            TitleAr = q.TitleAr,
            BodyEn = q.BodyEn,
            BodyAr = q.BodyAr,
            CategoryId = q.CategoryId,
            CategoryNameEn = q.CategoryId is { } cId && categoryNames.TryGetValue(cId, out var cn) ? cn.En : null,
            CategoryNameAr = q.CategoryId is { } cId2 && categoryNames.TryGetValue(cId2, out var cn2) ? cn2.Ar : null,
            ChannelId = q.ChannelId,
            ChannelNameEn = q.ChannelId is { } chId && channelNames.TryGetValue(chId, out var chn) ? chn.En : null,
            ChannelNameAr = q.ChannelId is { } chId2 && channelNames.TryGetValue(chId2, out var chn2) ? chn2.Ar : null,
            UsageCount = q.UsageCount,
            IsActive = q.IsActive,
            CanEdit = q.Scope == "Global"
                ? canManageGlobal
                : canManage && (q.Scope != "Personal" || q.OwnerId == currentUser.UserId),
        }).ToList();
    }
}

/// <summary>Shared by the list query and the render command, so "what can this caller see" is defined once.</summary>
internal static class QuickReplyVisibility
{
    public static async Task<IQueryable<QuickReply>> ApplyAsync(
        IQueryable<QuickReply> query, IAppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var teamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId && m.IsActive)
            .Select(m => m.TeamId)
            .ToListAsync(ct);

        return query.Where(q =>
            q.Scope == "Global" ||
            (q.Scope == "Personal" && q.OwnerId == currentUser.UserId) ||
            (q.Scope == "Team" && q.TeamId != null && teamIds.Contains(q.TeamId.Value)));
    }
}
