using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Auditing.Queries;

/// <summary>One row of the audit trail, as listed.</summary>
public record AuditLogListItemDto
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public AuditAction Action { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? IpAddress { get; init; }
}

/// <summary>
/// The audit trail, filtered and paged (Security &amp; Administration / Audit logs).
/// </summary>
/// <remarks>
/// Defaults to the last 7 days when no range is given: an unbounded scan of an audit table is the
/// classic way to take a database down. Page size is capped harder than the shared ceiling for the
/// same reason.
/// </remarks>
[RequirePermission(Permissions.Administration.ViewAuditLogs)]
public class GetAuditLogsQuery : PagedQuery, IRequest<PagedResult<AuditLogListItemDto>>
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public AuditAction? Action { get; set; }
}

public class GetAuditLogsQueryHandler(IAppDbContext db, IDateTimeProvider clock)
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogListItemDto>>
{
    /// <summary>Lower than the shared 200: audit rows are wide and this table is the biggest in the schema.</summary>
    private const int MaxPageSize = 100;

    internal const int DefaultWindowDays = 7;

    public async Task<PagedResult<AuditLogListItemDto>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? clock.UtcNow.AddDays(-DefaultWindowDays);
        var pageSize = Math.Min(request.PageSize, MaxPageSize);

        var query = db.AuditLogs.AsNoTracking().Where(a => a.OccurredAt >= from);

        if (request.To is { } to)
        {
            query = query.Where(a => a.OccurredAt <= to);
        }

        if (request.UserId is { } userId)
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(a => a.EntityType == request.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            query = query.Where(a => a.EntityId == request.EntityId);
        }

        if (request.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a =>
                (a.UserName != null && EF.Functions.Like(a.UserName, $"%{term}%")) ||
                EF.Functions.Like(a.EntityType, $"%{term}%") ||
                (a.EntityId != null && EF.Functions.Like(a.EntityId, $"%{term}%")) ||
                (a.IpAddress != null && EF.Functions.Like(a.IpAddress, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Newest first, always: the trail is read to answer "what just happened".
        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogListItemDto
            {
                Id = a.Id,
                OccurredAt = a.OccurredAt,
                UserId = a.UserId,
                UserName = a.UserName,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                IpAddress = a.IpAddress,
            })
            .ToListAsync(cancellationToken);

        return PagedResult<AuditLogListItemDto>.Create(items, request.Page, pageSize, totalCount);
    }
}

/// <summary>Distinct entity types present in the trail, for the filter dropdown.</summary>
[RequirePermission(Permissions.Administration.ViewAuditLogs)]
public record GetAuditEntityTypesQuery : IRequest<IReadOnlyList<string>>;

public class GetAuditEntityTypesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAuditEntityTypesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetAuditEntityTypesQuery request, CancellationToken cancellationToken) =>
        await db.AuditLogs.AsNoTracking()
            .Select(a => a.EntityType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);
}
