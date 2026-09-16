using System.Globalization;
using System.Text;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Auditing.Queries;

/// <summary>
/// Exports the filtered trail as CSV (Security &amp; Administration / Audit logs).
/// Requires the export permission on top of audit access, and records an <c>Export</c> entry —
/// browsing the log is not audited (that would flood it), but taking a copy out of the system is.
/// </summary>
[RequirePermission(Permissions.Administration.ViewAuditLogs)]
public class ExportAuditLogsQuery : IRequest<byte[]>
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? UserId { get; set; }
    public string? EntityType { get; set; }
    public AuditAction? Action { get; set; }
}

public class ExportAuditLogsQueryHandler(
    IAppDbContext db, IDateTimeProvider clock, IAuditRecorder audit, ICurrentUser currentUser)
    : IRequestHandler<ExportAuditLogsQuery, byte[]>
{
    /// <summary>Bounded so an export cannot become an unpaged dump of the whole table.</summary>
    private const int MaxRows = 10_000;

    public async Task<byte[]> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        // The attribute carries audit access; taking data out of the system needs the export
        // permission too, and the attribute only holds one key — so the second is checked here.
        if (!currentUser.HasPermission(Permissions.Reports.Export))
        {
            throw new Common.Exceptions.ForbiddenException(
                "Exporting the audit log also requires the reports export permission.");
        }

        var from = request.From ?? clock.UtcNow.AddDays(-GetAuditLogsQueryHandler.DefaultWindowDays);
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

        if (request.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        var rows = await query
            .OrderByDescending(a => a.OccurredAt)
            .Take(MaxRows)
            .Select(a => new
            {
                a.OccurredAt,
                a.UserName,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.IpAddress,
                a.CorrelationId,
            })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("OccurredAt,UserName,Action,EntityType,EntityId,IpAddress,CorrelationId");

        foreach (var row in rows)
        {
            csv.Append(row.OccurredAt.ToString("O", CultureInfo.InvariantCulture)).Append(',')
               .Append(Escape(row.UserName)).Append(',')
               .Append(row.Action).Append(',')
               .Append(Escape(row.EntityType)).Append(',')
               .Append(Escape(row.EntityId)).Append(',')
               .Append(Escape(row.IpAddress)).Append(',')
               .Append(Escape(row.CorrelationId))
               .AppendLine();
        }

        await audit.RecordAsync(
            AuditAction.Export,
            nameof(Domain.Identity.AuditLog),
            metadata: new { Rows = rows.Count, From = from, request.To, request.EntityType, request.Action },
            ct: cancellationToken);

        // UTF-8 BOM: without it Excel opens the file as the system codepage and mangles Arabic names.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>
    /// Quotes a CSV field. Also neutralises values a spreadsheet would execute as a formula — audit
    /// content is attacker-influenceable (a username, a user agent), and CSV injection is a real path
    /// from "read the log" to "run something on the reviewer's machine".
    /// </summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
