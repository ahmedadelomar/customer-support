using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Marks a waiting or active chat with no activity for <c>livechat.abandonTimeoutMinutes</c> as
/// Abandoned (Communication Channels / Live chat, CS-303), so the agent queue never fills with
/// sessions the visitor already walked away from. Runs every 5 minutes.
/// </summary>
[DisallowConcurrentExecution]
public class AbandonStaleChatSessionsJob(
    AppDbContext db,
    ISettingsProvider settings,
    IInteractionRecorder interactions,
    IChatRealtimeNotifier realtime,
    IDateTimeProvider clock,
    ILogger<AbandonStaleChatSessionsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var minutes = await settings.GetAsync(SettingKeys.LiveChatAbandonTimeoutMinutes, 10, null, ct);
        var cutoff = clock.UtcNow.AddMinutes(-minutes);

        // The open-session set is small by nature (only currently waiting/active chats), so loading
        // it with its messages and filtering in memory is simpler and provider-safe, unlike a
        // conditional aggregate that would need to translate identically to both SQLite and SQL Server.
        var candidates = await db.ChatSessions
            .Where(s => s.Status == "Waiting" || s.Status == "Active")
            .Include(s => s.Messages)
            .ToListAsync(ct);

        var stale = candidates
            .Where(s => (s.Messages.Count > 0 ? s.Messages.Max(m => m.SentAt) : s.StartedAt) <= cutoff)
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var session in stale)
        {
            session.Status = "Abandoned";
            session.EndedAt = now;
            session.MessageCount = session.Messages.Count;

            if (session.CustomerId is { } customerId)
            {
                interactions.Record(
                    customerId, ChannelKey.LiveChat, MessageDirection.Inbound,
                    "Live chat abandoned", "The visitor left without a reply.", null, "ChatSession", session.Id);
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var session in stale)
        {
            var dto = ChatMapper.ToDto(session, null, 0);
            await realtime.PushToSessionAsync(session.Id, "session.abandoned", dto, ct);
            if (session.QueuedForTeamId is { } teamId)
            {
                await realtime.PushToTeamQueueAsync(teamId, "session.abandoned", dto, ct);
            }
        }

        logger.LogInformation("Abandoned {Count} stale chat session(s).", stale.Count);
    }
}
