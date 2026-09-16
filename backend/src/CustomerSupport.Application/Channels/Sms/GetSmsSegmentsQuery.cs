using CustomerSupport.Application.Common.Security;
using MediatR;

namespace CustomerSupport.Application.Channels.Sms;

/// <summary>
/// Server-authoritative segment count for a composer's live counter (Communication Channels / SMS
/// channel, CS-304) — the client mirrors the same charset tables for responsiveness, but this is
/// what the agent and the server must agree on before a send is ever attempted.
/// </summary>
[RequirePermission(Permissions.Tickets.Reply)]
public record GetSmsSegmentsQuery(string Text) : IRequest<SmsSegments>;

public class GetSmsSegmentsQueryHandler : IRequestHandler<GetSmsSegmentsQuery, SmsSegments>
{
    public Task<SmsSegments> Handle(GetSmsSegmentsQuery request, CancellationToken ct) =>
        Task.FromResult(SmsSegmentCalculator.Calculate(request.Text));
}
