using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace CustomerSupport.Api.Hubs;

/// <summary>One viewer's presence within a ticket group, as broadcast to the group.</summary>
public record PresenceEntry(Guid UserId, string DisplayName, bool IsComposing);

/// <summary>
/// In-memory presence and composing state for <see cref="CollaborationHub"/> — deliberately never
/// persisted (the story's own rule: "presence is ephemeral, hub state only"). A composing flag expires
/// 30 seconds after the last keystroke, per the story, so a closed laptop does not show as permanently
/// composing. Registered as a singleton: presence must be shared across every hub instance (SignalR
/// creates one per method invocation), not scoped per call.
/// </summary>
public sealed class PresenceTracker : IDisposable
{
    private static readonly TimeSpan ComposingExpiry = TimeSpan.FromSeconds(30);

    private sealed class Viewer
    {
        public Guid UserId;
        public string DisplayName = "";
        public DateTimeOffset? ComposingSince;
    }

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Viewer>> _groupViewers = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();
    private readonly IHubContext<CollaborationHub> _hub;
    private readonly Timer _expiryTimer;

    public PresenceTracker(IHubContext<CollaborationHub> hub)
    {
        _hub = hub;
        _expiryTimer = new Timer(_ => ExpireComposing(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async Task JoinAsync(string group, string connectionId, Guid userId, string displayName)
    {
        var viewers = _groupViewers.GetOrAdd(group, _ => new ConcurrentDictionary<string, Viewer>());
        viewers[connectionId] = new Viewer { UserId = userId, DisplayName = displayName };

        _connectionGroups.AddOrUpdate(
            connectionId,
            _ => [group],
            (_, groups) => { lock (groups) { groups.Add(group); } return groups; });

        await BroadcastAsync(group);
    }

    public async Task LeaveAsync(string group, string connectionId)
    {
        if (_groupViewers.TryGetValue(group, out var viewers))
        {
            viewers.TryRemove(connectionId, out _);
        }

        if (_connectionGroups.TryGetValue(connectionId, out var groups))
        {
            lock (groups) { groups.Remove(group); }
        }

        await BroadcastAsync(group);
    }

    /// <summary>Removes a dropped connection from every group it was in, broadcasting each. Called from <c>OnDisconnectedAsync</c>.</summary>
    public async Task RemoveConnectionAsync(string connectionId)
    {
        if (!_connectionGroups.TryRemove(connectionId, out var groups))
        {
            return;
        }

        string[] snapshot;
        lock (groups) { snapshot = [.. groups]; }

        foreach (var group in snapshot)
        {
            if (_groupViewers.TryGetValue(group, out var viewers))
            {
                viewers.TryRemove(connectionId, out _);
            }

            await BroadcastAsync(group);
        }
    }

    public async Task SetComposingAsync(string group, string connectionId, bool isComposing)
    {
        if (!_groupViewers.TryGetValue(group, out var viewers) || !viewers.TryGetValue(connectionId, out var viewer))
        {
            return;
        }

        viewer.ComposingSince = isComposing ? DateTimeOffset.UtcNow : null;
        await BroadcastAsync(group);
    }

    private async void ExpireComposing()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (group, viewers) in _groupViewers)
        {
            var expired = false;
            foreach (var viewer in viewers.Values)
            {
                if (viewer.ComposingSince is { } since && now - since > ComposingExpiry)
                {
                    viewer.ComposingSince = null;
                    expired = true;
                }
            }

            if (expired)
            {
                await BroadcastAsync(group);
            }
        }
    }

    private Task BroadcastAsync(string group)
    {
        var entries = _groupViewers.TryGetValue(group, out var viewers)
            ? viewers.Values.Select(v => new PresenceEntry(v.UserId, v.DisplayName, v.ComposingSince is not null)).ToList()
            : [];

        return _hub.Clients.Group(group).SendAsync("presenceUpdated", entries);
    }

    public void Dispose() => _expiryTimer.Dispose();
}
