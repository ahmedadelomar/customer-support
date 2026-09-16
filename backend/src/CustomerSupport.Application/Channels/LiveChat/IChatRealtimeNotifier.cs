namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Pushes live-chat events over SignalR. Implemented in the Api project (like <c>IRealtimeNotifier</c>
/// for notifications) because only Api references <c>IHubContext</c> — Application stays transport-agnostic.
/// </summary>
public interface IChatRealtimeNotifier
{
    /// <summary>Broadcasts to everyone viewing this one session (<c>chat:{sessionId}</c>).</summary>
    Task PushToSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken ct = default);

    /// <summary>Broadcasts to agents watching a team's queue (<c>chatqueue:{teamId}</c>).</summary>
    Task PushToTeamQueueAsync(Guid teamId, string eventName, object payload, CancellationToken ct = default);
}
