using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Infrastructure.Services;

/// <summary>
/// Preference resolution, quiet hours and the always-created in-app row behind alerts and
/// notifications (CS-504).
/// </summary>
public class NotificationDispatcherTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private DateTimeOffset _now;
    private NotificationDispatcher _dispatcher = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            UserName = "agent1",
            Email = "agent1@example.com",
            IsActive = true,
            PreferredLanguage = "en",
            TimeZoneId = "Asia/Riyadh",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _userId = user.Id;

        _now = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero); // 13:00 in Asia/Riyadh (+3)
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => _now);

        _dispatcher = new NotificationDispatcher(_db, clock);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task InAppRow_IsAlwaysCreated_RegardlessOfPreferences()
    {
        await _dispatcher.DispatchAsync(_userId, "ticket.assigned", "T", "ت", "B", "ب");
        await _db.SaveChangesAsync();

        _db.Notifications.Single(n => n.UserId == _userId).EventType.Should().Be("ticket.assigned");
    }

    [Fact]
    public async Task DeactivatedUser_ReceivesNothing()
    {
        var deactivated = new ApplicationUser { UserName = "gone", IsActive = false };
        _db.Users.Add(deactivated);
        await _db.SaveChangesAsync();

        await _dispatcher.DispatchAsync(deactivated.Id, "ticket.assigned", "T", "ت", "B", "ب");
        await _db.SaveChangesAsync();

        _db.Notifications.Any(n => n.UserId == deactivated.Id).Should().BeFalse();
        _db.OutboxMessages.Any().Should().BeFalse();
    }

    [Fact]
    public async Task MissingPreferenceRow_UsesTheDocumentedDefault()
    {
        // "sla.breached" defaults to email on; "sla.warning" defaults to in-app only.
        await _dispatcher.DispatchAsync(_userId, "sla.breached", "T", "ت", "B", "ب", severity: "Critical");
        await _db.SaveChangesAsync();

        _db.OutboxMessages.Should().ContainSingle(o => o.Type == "notification.email");
    }

    [Fact]
    public async Task MissingPreferenceRow_NoDefaultChannel_QueuesNoOutboxRow()
    {
        await _dispatcher.DispatchAsync(_userId, "sla.warning", "T", "ت", "B", "ب");
        await _db.SaveChangesAsync();

        _db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task QuietHours_DefersExternalDelivery_ButNotTheInAppRow()
    {
        _db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = _userId,
            EventType = "ticket.assigned",
            ViaInApp = true,
            ViaEmail = true,
            QuietHoursStart = new TimeOnly(9, 0),
            QuietHoursEnd = new TimeOnly(17, 0),
        });
        await _db.SaveChangesAsync();

        // _now is 13:00 Asia/Riyadh — inside the 09:00-17:00 quiet window.
        await _dispatcher.DispatchAsync(_userId, "ticket.assigned", "T", "ت", "B", "ب");
        await _db.SaveChangesAsync();

        _db.Notifications.Should().ContainSingle(n => n.UserId == _userId);

        var outbox = _db.OutboxMessages.Single(o => o.Type == "notification.email");
        outbox.NextAttemptAt.Should().BeAfter(_now);
        outbox.NextAttemptAt!.Value.Should().Be(new DateTimeOffset(2026, 9, 13, 14, 0, 0, TimeSpan.Zero)); // 17:00 Riyadh = 14:00 UTC
    }

    [Fact]
    public async Task CriticalSeverity_BypassesQuietHours()
    {
        _db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = _userId,
            EventType = "sla.breached",
            ViaInApp = true,
            ViaEmail = true,
            QuietHoursStart = new TimeOnly(9, 0),
            QuietHoursEnd = new TimeOnly(17, 0),
        });
        await _db.SaveChangesAsync();

        await _dispatcher.DispatchAsync(_userId, "sla.breached", "T", "ت", "B", "ب", severity: "Critical");
        await _db.SaveChangesAsync();

        var outbox = _db.OutboxMessages.Single(o => o.Type == "notification.email");
        outbox.NextAttemptAt.Should().Be(_now);
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNoNotificationRow()
    {
        await using var db2 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        var dispatcher = new NotificationDispatcher(db2, clock);

        await using (var tx = await db2.Database.BeginTransactionAsync())
        {
            await dispatcher.DispatchAsync(_userId, "ticket.assigned", "T", "ت", "B", "ب");
            await db2.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        _db.Notifications.Any(n => n.UserId == _userId).Should().BeFalse();
    }
}
