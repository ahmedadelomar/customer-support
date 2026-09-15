using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Services;

/// <summary>
/// Covers <see cref="InteractionRecorder"/> directly, since nothing in the product yet calls it —
/// ticket creation (CS-201) and the channel pipeline (Section 3) are the eventual callers, and
/// neither is built. This is the only place the recorder's behaviour is currently exercised.
/// </summary>
public class InteractionRecorderTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IDateTimeProvider FixedClock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static Customer NewCustomer(string code) => new()
    {
        Code = code,
        DisplayName = new LocalizedText("Test Customer", "عميل تجريبي"),
        PreferredLanguage = "en",
    };

    [Fact]
    public async Task Record_StripsHtmlAndTruncatesPreviewAtOneThousandCharacters()
    {
        await using var db = CreateContext();
        var customer = NewCustomer("CUS-100001");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var recorder = new InteractionRecorder(db, FixedClock(DateTimeOffset.UtcNow));
        // Tags are replaced with a space each (not deleted outright), so "<b>world</b>." correctly
        // becomes "world ." rather than gluing the two words either side of a tag together.
        var htmlBody = "<p>Hello&nbsp;<b>world</b> done.</p>" + new string('x', 2000);

        recorder.Record(customer.Id, ChannelKey.Email, MessageDirection.Inbound, "Subject", htmlBody);
        await db.SaveChangesAsync();

        var saved = await db.Interactions.SingleAsync();

        saved.Preview.Should().NotBeNull();
        saved.Preview.Should().NotContain("<").And.NotContain(">");
        saved.Preview!.Length.Should().Be(1000);
        saved.Preview.Should().StartWith("Hello world done.");
    }

    [Fact]
    public async Task Record_WithNullOrBlankPreview_StoresNullRatherThanAnEmptyString()
    {
        await using var db = CreateContext();
        var customer = NewCustomer("CUS-100002");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var recorder = new InteractionRecorder(db, FixedClock(DateTimeOffset.UtcNow));
        recorder.Record(customer.Id, ChannelKey.Portal, MessageDirection.Outbound, null, "   ");
        await db.SaveChangesAsync();

        var saved = await db.Interactions.SingleAsync();
        saved.Preview.Should().BeNull();
    }

    [Fact]
    public async Task Record_UpdatesLastInteractionAt_WhenCustomerIsNotAlreadyTrackedInThisUnitOfWork()
    {
        // Simulates the common case: a background job or a handler that never loaded the full
        // Customer entity, only knows its id.
        await using var db = CreateContext();
        var customer = NewCustomer("CUS-100003");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var recorder = new InteractionRecorder(db, FixedClock(now));

        recorder.Record(customer.Id, ChannelKey.WebForm, MessageDirection.Inbound, null, null);
        await db.SaveChangesAsync();

        var reloaded = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id);
        reloaded.LastInteractionAt.Should().Be(now);
    }

    [Fact]
    public async Task Record_UpdatesLastInteractionAt_WhenCustomerIsAlreadyTrackedInThisUnitOfWork()
    {
        // Simulates a handler that loaded and is still holding the Customer entity — attaching a
        // second, fresh instance with the same key would throw ("already being tracked").
        await using var db = CreateContext();
        var customer = NewCustomer("CUS-100004");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        // customer instance stays tracked here, unlike the test above

        var now = new DateTimeOffset(2026, 2, 2, 8, 0, 0, TimeSpan.Zero);
        var recorder = new InteractionRecorder(db, FixedClock(now));

        var act = () => recorder.Record(customer.Id, ChannelKey.Sms, MessageDirection.Outbound, null, null);
        act.Should().NotThrow();
        await db.SaveChangesAsync();

        customer.LastInteractionAt.Should().Be(now);
    }

    [Fact]
    public async Task Record_DoesNotSaveOnItsOwn_CallerMustCallSaveChanges()
    {
        await using var db = CreateContext();
        var customer = NewCustomer("CUS-100005");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var recorder = new InteractionRecorder(db, FixedClock(DateTimeOffset.UtcNow));
        recorder.Record(customer.Id, ChannelKey.Email, MessageDirection.Inbound, null, null);

        // No SaveChangesAsync call — a fresh context reading the same in-memory database must see nothing yet.
        (await db.Interactions.CountAsync()).Should().Be(0, "the row is only pending in the change tracker");
        db.ChangeTracker.Entries<Interaction>().Should().HaveCount(1);
    }
}
