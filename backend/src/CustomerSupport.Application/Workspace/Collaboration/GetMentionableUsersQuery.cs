using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>
/// Candidates for the "@" mention picker, filtered as the author types. Every active agent matching
/// the search text is returned — never narrowed to just the visible set — with <see cref="MentionableUserDto.CanView"/>
/// telling the editor which ones would actually see the ticket, per the story's "warn, don't exclude" rule.
/// </summary>
[RequirePermission(Permissions.Workspace.Collaborate)]
public record GetMentionableUsersQuery(Guid TicketId, string? Search) : IRequest<IReadOnlyList<MentionableUserDto>>;

public class GetMentionableUsersQueryHandler(IAppDbContext db, ICurrentUser currentUser, IAgentDirectory agents)
    : IRequestHandler<GetMentionableUsersQuery, IReadOnlyList<MentionableUserDto>>
{
    public async Task<IReadOnlyList<MentionableUserDto>> Handle(
        GetMentionableUsersQuery request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        var candidates = await agents.SearchActiveAsync(request.Search, limit: 20, cancellationToken);
        var candidatesWithViewAll = await agents.FilterByPermissionAsync(
            candidates.Select(c => c.Id), Permissions.Tickets.ViewAll, cancellationToken);

        return candidates
            .Where(c => c.Id != currentUser.UserId)
            .Select(c => new MentionableUserDto
            {
                Id = c.Id,
                NameEn = c.DisplayName.En,
                NameAr = c.DisplayName.Ar,
                CanView =
                    candidatesWithViewAll.Contains(c.Id) ||
                    c.Id == ticket.AssignedAgentId ||
                    (ticket.DepartmentId is { } deptId && c.DepartmentId == deptId),
            })
            .ToList();
    }
}
