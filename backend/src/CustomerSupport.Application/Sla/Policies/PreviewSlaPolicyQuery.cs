using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Sla;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Sla.Policies;

/// <summary>
/// Computes the due times a policy would produce for a hypothetical ticket, without creating one —
/// how a manager sanity-checks a policy before it affects real tickets.
/// </summary>
[RequirePermission(Permissions.Sla.ManagePolicies)]
public record PreviewSlaPolicyQuery(Guid PolicyId, Guid PriorityId, DateTimeOffset ArrivalTime)
    : IRequest<SlaPolicyPreviewResult>;

public record SlaPolicyPreviewResult(DateTimeOffset? FirstResponseDueAt, DateTimeOffset? ResolutionDueAt, string? Note);

public class PreviewSlaPolicyQueryHandler(IAppDbContext db, IBusinessCalendarCalculator calendar)
    : IRequestHandler<PreviewSlaPolicyQuery, SlaPolicyPreviewResult>
{
    public async Task<SlaPolicyPreviewResult> Handle(PreviewSlaPolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await db.SlaPolicies
            .Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken)
            ?? throw new NotFoundException(nameof(SlaPolicy), request.PolicyId);

        var target = policy.Targets.FirstOrDefault(t => t.PriorityId == request.PriorityId);
        if (target is null)
        {
            return new SlaPolicyPreviewResult(null, null, "This policy has no target for the selected priority — a ticket at this priority would get no SLA clocks.");
        }

        var firstResponseDue = await calendar.AddWorkingMinutesAsync(
            policy.BusinessCalendarId, request.ArrivalTime, target.FirstResponseMinutes, cancellationToken);
        var resolutionDue = await calendar.AddWorkingMinutesAsync(
            policy.BusinessCalendarId, request.ArrivalTime, target.ResolutionMinutes, cancellationToken);

        return new SlaPolicyPreviewResult(firstResponseDue, resolutionDue, null);
    }
}
