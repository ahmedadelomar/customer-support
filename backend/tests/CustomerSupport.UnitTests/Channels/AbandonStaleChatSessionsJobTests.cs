using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

public class AbandonStaleChatSessionsJobTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task StaleWaitingAndActiveSessions_AreMarkedAbandoned_RecentOnesAreNot()
    {
        var now = DateTimeOffset.UtcNow;

        var stale = new ChatSession
        {
            VisitorKey = "stale", Status = "Waiting", StartedAt = now.AddMinutes(-20),
        };
        stale.Messages.Add(new ChatMessage { ChatSessionId = stale.Id, AuthorType = MessageAuthorType.Customer, Body = "hi", SentAt = now.AddMinutes(-20) });

        var recent = new ChatSession
        {
            VisitorKey = "recent", Status = "Active", StartedAt = now.AddMinutes(-20),
        };
        recent.Messages.Add(new ChatMessage { ChatSessionId = recent.Id, AuthorType = MessageAuthorType.Agent, Body = "still here", SentAt = now.AddMinutes(-1) });

        _db.ChatSessions.AddRange(stale, recent);
        await _db.SaveChangesAsync();

        var settings = Substitute.For<ISettingsProvider>();
        settings.GetAsync(SettingKeys.LiveChatAbandonTimeoutMinutes, Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(10);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);

        var job = new AbandonStaleChatSessionsJob(
            _db, settings, Substitute.For<IInteractionRecorder>(), Substitute.For<IChatRealtimeNotifier>(),
            clock, NullLogger<AbandonStaleChatSessionsJob>.Instance);

        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);

        await job.Execute(context);

        (await _db.ChatSessions.FindAsync(stale.Id))!.Status.Should().Be("Abandoned");
        (await _db.ChatSessions.FindAsync(recent.Id))!.Status.Should().Be("Active");
    }
}
