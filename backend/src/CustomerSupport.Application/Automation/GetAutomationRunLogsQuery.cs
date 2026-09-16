using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation;

public record AutomationRunLogDto(
    Guid Id, string RuleType, Guid RuleId, string? RuleName, Guid TicketId, string? TicketNumber,
    string Outcome, string? Reason, string? Error, int DurationMs, DateTimeOffset OccurredAt);

/// <summary>
/// The decision log viewer shared by assignment (CS-502) and escalation (CS-503) rules — "why did
/// this go to Ahmed" and "why did this escalate" are the same screen with a different filter.
/// </summary>
[RequirePermission(Permissions.Sla.View)]
public record GetAutomationRunLogsQuery : IRequest<IReadOnlyList<AutomationRunLogDto>>
{
    public Guid? TicketId { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleType { get; init; }
    public string? Outcome { get; init; }
    public int Take { get; init; } = 200;
}

public class GetAutomationRunLogsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAutomationRunLogsQuery, IReadOnlyList<AutomationRunLogDto>>
{
    public async Task<IReadOnlyList<AutomationRunLogDto>> Handle(GetAutomationRunLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AutomationRunLogs.AsNoTracking().AsQueryable();

        if (request.TicketId is { } ticketId)
        {
            query = query.Where(l => l.TicketId == ticketId);
        }

        if (request.RuleId is { } ruleId)
        {
            query = query.Where(l => l.RuleId == ruleId);
        }

        if (!string.IsNullOrWhiteSpace(request.RuleType))
        {
            query = query.Where(l => l.RuleType == request.RuleType);
        }

        if (!string.IsNullOrWhiteSpace(request.Outcome))
        {
            query = query.Where(l => l.Outcome == request.Outcome);
        }

        var logs = await query
            .OrderByDescending(l => l.OccurredAt)
            .Take(Math.Clamp(request.Take, 1, 500))
            .ToListAsync(cancellationToken);

        var ticketIds = logs.Select(l => l.TicketId).Distinct().ToList();
        var ticketNumbers = await db.Tickets.AsNoTracking()
            .Where(t => ticketIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Number })
            .ToDictionaryAsync(t => t.Id, t => t.Number, cancellationToken);

        return logs.Select(l => new AutomationRunLogDto(
            l.Id, l.RuleType, l.RuleId, l.RuleName, l.TicketId, ticketNumbers.GetValueOrDefault(l.TicketId),
            l.Outcome, l.Reason, l.Error, l.DurationMs, l.OccurredAt)).ToList();
    }
}
