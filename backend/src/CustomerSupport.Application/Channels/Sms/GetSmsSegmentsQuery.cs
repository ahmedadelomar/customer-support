using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Common.Settings;
using MediatR;

namespace CustomerSupport.Application.Channels.Sms;

/// <summary>Adds the configured cap to <see cref="SmsSegments"/>, so the composer can colour and disable itself against the same number the server will actually enforce.</summary>
public record SmsSegmentsResult(int Length, int SegmentCount, string Encoding, int MaxLength, int MaxSegments);

/// <summary>
/// Server-authoritative segment count for a composer's live counter (Communication Channels / SMS
/// channel, CS-304) — the client mirrors the same charset tables for responsiveness, but this is
/// what the agent and the server must agree on before a send is ever attempted.
/// </summary>
[RequirePermission(Permissions.Tickets.Reply)]
public record GetSmsSegmentsQuery(string Text, Guid? BranchId) : IRequest<SmsSegmentsResult>;

public class GetSmsSegmentsQueryHandler(ISettingsProvider settings) : IRequestHandler<GetSmsSegmentsQuery, SmsSegmentsResult>
{
    public async Task<SmsSegmentsResult> Handle(GetSmsSegmentsQuery request, CancellationToken ct)
    {
        var segments = SmsSegmentCalculator.Calculate(request.Text);
        var maxSegments = await settings.GetAsync(SettingKeys.SmsMaxSegments, 3, request.BranchId, ct);

        return new SmsSegmentsResult(segments.Length, segments.SegmentCount, segments.Encoding, segments.MaxLength, maxSegments);
    }
}
