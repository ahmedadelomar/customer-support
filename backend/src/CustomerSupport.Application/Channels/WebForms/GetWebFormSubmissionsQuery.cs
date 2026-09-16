using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.WebForms;

[RequirePermission(Permissions.Channels.ManageWebForms)]
public record GetWebFormSubmissionsQuery(Guid WebFormId) : IRequest<IReadOnlyList<WebFormSubmissionDto>>;

public class GetWebFormSubmissionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetWebFormSubmissionsQuery, IReadOnlyList<WebFormSubmissionDto>>
{
    public async Task<IReadOnlyList<WebFormSubmissionDto>> Handle(GetWebFormSubmissionsQuery request, CancellationToken ct)
    {
        var submissions = await db.WebFormSubmissions
            .Where(s => s.WebFormDefinitionId == request.WebFormId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);

        var ticketIds = submissions.Where(s => s.TicketId != null).Select(s => s.TicketId!.Value).ToList();
        var ticketNumbers = await db.Tickets
            .Where(t => ticketIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Number })
            .ToDictionaryAsync(t => t.Id, t => t.Number, ct);

        return submissions.Select(s => new WebFormSubmissionDto(
            s.Id, s.WebFormDefinitionId, s.SubmitterName, s.SubmitterEmail, s.SubmitterPhone,
            s.Status, s.FailureReason, s.TicketId,
            s.TicketId is { } id ? ticketNumbers.GetValueOrDefault(id) : null,
            s.SubmittedAt, s.ProcessedAt, s.PayloadJson)).ToList();
    }
}
