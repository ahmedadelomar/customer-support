using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Application.Files.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Notes;

/// <summary>Paged notes for one customer, pinned first, newest first within each group.</summary>
[RequirePermission(Permissions.Customers.ViewNotes)]
public record GetCustomerNotesQuery : IRequest<PagedResult<CustomerNoteDto>>
{
    public Guid CustomerId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetCustomerNotesQueryValidator : AbstractValidator<GetCustomerNotesQuery>
{
    public GetCustomerNotesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetCustomerNotesQueryHandler(IAppDbContext db, ICurrentUser currentUser, IUserDisplayNameResolver userNames)
    : IRequestHandler<GetCustomerNotesQuery, PagedResult<CustomerNoteDto>>
{
    public async Task<PagedResult<CustomerNoteDto>> Handle(GetCustomerNotesQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Customers.Customer), request.CustomerId);
        }

        var query = db.CustomerNotes.AsNoTracking().Where(n => n.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new
            {
                n.Id,
                n.CustomerId,
                n.TicketId,
                n.Body,
                n.IsPinned,
                n.IsInternal,
                n.CreatedById,
                n.CreatedAt,
                n.ModifiedAt,
            })
            .ToListAsync(cancellationToken);

        var noteIds = rows.Select(r => r.Id).ToList();

        var attachmentsByNote = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == "CustomerNote" && noteIds.Contains(a.OwnerId))
            .Select(a => new { a.OwnerId, Dto = new AttachmentDto
            {
                Id = a.Id, FileName = a.FileName, ContentType = a.ContentType,
                SizeBytes = a.SizeBytes, CreatedAt = a.CreatedAt,
            }})
            .ToListAsync(cancellationToken);

        var attachmentLookup = attachmentsByNote
            .GroupBy(a => a.OwnerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AttachmentDto>)g.Select(a => a.Dto).ToList());

        var authorIds = rows.Where(r => r.CreatedById is not null).Select(r => r.CreatedById!.Value).Distinct();
        var authorNames = await userNames.ResolveAsync(authorIds, cancellationToken);

        var items = rows.Select(row => new CustomerNoteDto
        {
            Id = row.Id,
            CustomerId = row.CustomerId,
            TicketId = row.TicketId,
            Body = row.Body,
            IsPinned = row.IsPinned,
            IsInternal = row.IsInternal,
            CreatedById = row.CreatedById,
            AuthorNameEn = row.CreatedById is { } authorId && authorNames.TryGetValue(authorId, out var name) ? name.En : null,
            AuthorNameAr = row.CreatedById is { } authorId2 && authorNames.TryGetValue(authorId2, out var name2) ? name2.Ar : null,
            CreatedAt = row.CreatedAt,
            ModifiedAt = row.ModifiedAt,
            CanEdit = row.CreatedById == currentUser.UserId || currentUser.HasPermission(Permissions.Customers.ManageNotes),
            Attachments = attachmentLookup.TryGetValue(row.Id, out var list) ? list : [],
        }).ToList();

        return PagedResult<CustomerNoteDto>.Create(items, request.Page, request.PageSize, totalCount);
    }
}
