using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>The caller's own mentions inbox, unread first — drives the top-bar badge and <c>/agent/mentions</c>.</summary>
[RequirePermission(Permissions.Workspace.Collaborate)]
public record GetMentionsQuery : IRequest<IReadOnlyList<MentionDto>>;

public class GetMentionsQueryHandler(IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetMentionsQuery, IReadOnlyList<MentionDto>>
{
    public async Task<IReadOnlyList<MentionDto>> Handle(GetMentionsQuery request, CancellationToken cancellationToken)
    {
        // TicketMention is a loose-reference entity (no navigation properties, same pattern as
        // AgentTask/Interaction), so the ticket and message it points at are joined explicitly rather
        // than read off a navigation.
        var rows = await db.TicketMentions.AsNoTracking()
            .Where(m => m.MentionedUserId == currentUser.UserId)
            .Join(db.Tickets, m => m.TicketId, t => t.Id, (m, t) => new { Mention = m, Ticket = t })
            .Join(db.TicketMessages, x => x.Mention.TicketMessageId, msg => msg.Id, (x, msg) => new
            {
                x.Mention.Id,
                x.Mention.TicketId,
                x.Mention.MentionedById,
                x.Mention.MentionedAt,
                x.Mention.ReadAt,
                TicketNumber = x.Ticket.Number,
                TicketSubject = x.Ticket.Subject,
                MessageBody = msg.BodyText,
            })
            .OrderBy(r => r.ReadAt != null)
            .ThenByDescending(r => r.MentionedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await userNames.ResolveAsync(rows.Select(r => r.MentionedById).Distinct(), cancellationToken);

        return rows.Select(r =>
        {
            var name = names.GetValueOrDefault(r.MentionedById);
            return new MentionDto
            {
                Id = r.Id,
                TicketId = r.TicketId,
                TicketNumber = r.TicketNumber,
                TicketSubject = r.TicketSubject,
                MentionedById = r.MentionedById,
                MentionedByNameEn = name?.En ?? "",
                MentionedByNameAr = name?.Ar ?? "",
                ExcerptEn = Truncate(MentionParser.ToPlainText(r.MessageBody)),
                MentionedAt = r.MentionedAt,
                ReadAt = r.ReadAt,
            };
        }).ToList();
    }

    private static string Truncate(string text, int maxLength = 160) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
