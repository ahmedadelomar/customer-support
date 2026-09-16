using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Files.Dtos;
using CustomerSupport.Application.Tickets.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Queries;

/// <summary>
/// Paged conversation thread, oldest first — the order the centre column of the detail screen
/// renders in. Internal notes are included: this endpoint is agent-only. A future portal-facing
/// endpoint must filter <c>IsInternalNote</c> out; see the class remark on <see cref="Domain.Tickets.TicketMessage"/>.
/// </summary>
[RequirePermission(Permissions.Tickets.View)]
public record GetTicketMessagesQuery : IRequest<PagedResult<TicketMessageDto>>
{
    public Guid TicketId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetTicketMessagesQueryValidator : AbstractValidator<GetTicketMessagesQuery>
{
    public GetTicketMessagesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public class GetTicketMessagesQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetTicketMessagesQuery, PagedResult<TicketMessageDto>>
{
    public async Task<PagedResult<TicketMessageDto>> Handle(GetTicketMessagesQuery request, CancellationToken cancellationToken)
    {
        var visible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.TicketId);
        }

        var query = db.TicketMessages.AsNoTracking().Where(m => m.TicketId == request.TicketId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(m => m.SentAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var messageIds = rows.Select(r => r.Id).ToList();

        var attachmentsByMessage = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == "TicketMessage" && messageIds.Contains(a.OwnerId))
            .Select(a => new
            {
                a.OwnerId,
                Dto = new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    SizeBytes = a.SizeBytes,
                    CreatedAt = a.CreatedAt,
                },
            })
            .ToListAsync(cancellationToken);

        var attachmentLookup = attachmentsByMessage
            .GroupBy(a => a.OwnerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AttachmentDto>)g.Select(a => a.Dto).ToList());

        // The most recent delivery attempt per message — an agent-facing indicator, so only the
        // latest status (not the whole retry history) is worth carrying to the thread. Reduced
        // client-side (like the attachment lookup above) rather than a GroupBy().First() query,
        // which translates unevenly across the two relational providers this app supports.
        var deliveryLogs = await db.MessageDeliveryLogs.AsNoTracking()
            .Where(l => messageIds.Contains(l.TicketMessageId))
            .ToListAsync(cancellationToken);

        var deliveryByMessage = deliveryLogs
            .GroupBy(l => l.TicketMessageId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.UpdatedAt).First());

        var items = rows.Select(m => new TicketMessageDto
        {
            Id = m.Id,
            TicketId = m.TicketId,
            Channel = m.Channel,
            Direction = m.Direction,
            AuthorType = m.AuthorType,
            AuthorId = m.AuthorId,
            AuthorDisplayName = m.AuthorDisplayName,
            Subject = m.Subject,
            BodyText = m.BodyText,
            BodyHtml = m.BodyHtml,
            IsInternalNote = m.IsInternalNote,
            SentAt = m.SentAt,
            Attachments = attachmentLookup.TryGetValue(m.Id, out var list) ? list : [],
            DeliveryStatus = deliveryByMessage.TryGetValue(m.Id, out var log) ? log.Status.ToString() : null,
            DeliveryError = deliveryByMessage.TryGetValue(m.Id, out var errorLog) ? errorLog.ErrorMessage : null,
        }).ToList();

        return PagedResult<TicketMessageDto>.Create(items, request.Page, request.PageSize, totalCount);
    }
}
