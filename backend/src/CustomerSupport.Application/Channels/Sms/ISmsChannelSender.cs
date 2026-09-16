using CustomerSupport.Application.Channels.Outbound;

namespace CustomerSupport.Application.Channels.Sms;

/// <summary>
/// Delivers one outbound customer-facing SMS through whichever provider a mailbox
/// (<c>ChannelAccount</c>) is configured with (Communication Channels / SMS channel, CS-304). Same
/// placeholder-until-a-real-provider shape as <c>IEmailChannelSender</c> — the outbox handler is
/// built against this interface so swapping in a real provider later is a DI registration change.
/// </summary>
public interface ISmsChannelSender
{
    Task<ChannelSendResult> SendAsync(string toPhone, string fromIdentifier, string body, CancellationToken ct = default);
}
