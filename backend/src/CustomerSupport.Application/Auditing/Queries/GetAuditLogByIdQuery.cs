using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Auditing.Queries;

/// <summary>One field's before and after, already parsed so the client renders a diff rather than JSON.</summary>
public record FieldChangeDto(string Field, string? OldValue, string? NewValue);

/// <summary>A full audit entry with its parsed diff.</summary>
public record AuditLogDetailDto
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public AuditAction Action { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? CorrelationId { get; init; }
    public IReadOnlyList<FieldChangeDto> Changes { get; init; } = [];
}

/// <summary>One audit entry, with <c>OldValues</c>/<c>NewValues</c> flattened into a field-level diff.</summary>
[RequirePermission(Permissions.Administration.ViewAuditLogs)]
public record GetAuditLogByIdQuery(Guid Id) : IRequest<AuditLogDetailDto>;

public class GetAuditLogByIdQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAuditLogByIdQuery, AuditLogDetailDto>
{
    public async Task<AuditLogDetailDto> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var entry = await db.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AuditLog), request.Id);

        return new AuditLogDetailDto
        {
            Id = entry.Id,
            OccurredAt = entry.OccurredAt,
            UserId = entry.UserId,
            UserName = entry.UserName,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            CorrelationId = entry.CorrelationId,
            Changes = BuildDiff(entry.OldValues, entry.NewValues),
        };
    }

    /// <summary>
    /// Pairs the two JSON blobs by property name. Only fields present in either side appear, so a
    /// create shows every new value and a delete every old one, without the client parsing JSON.
    /// </summary>
    private static List<FieldChangeDto> BuildDiff(string? oldValues, string? newValues)
    {
        var before = Parse(oldValues);
        var after = Parse(newValues);

        return before.Keys
            .Union(after.Keys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(key => new FieldChangeDto(key, before.GetValueOrDefault(key), after.GetValueOrDefault(key)))
            .ToList();
    }

    private static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return document.RootElement.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.ToString(),
                StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // A malformed blob must not break the viewer — the row still has actor, action and time.
            return [];
        }
    }
}
