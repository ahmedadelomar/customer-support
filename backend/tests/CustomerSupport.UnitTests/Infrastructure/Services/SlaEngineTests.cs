using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Sla;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Infrastructure.Services;

/// <summary>
/// The state machine behind every SLA due date (CS-501): policy selection and upsert, first-reply
/// Met/Breached, pause/resume across statuses, and resolution. A real Sqlite connection is used
/// because the engine and the calculator it drives both need a genuine relational context.
/// </summary>
public class SlaEngineTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private SlaEngine _engine = null!;
    private DateTimeOffset _now;
    private Guid _calendarId;
    private Guid _normalPriorityId;
    private Guid _pendingStatusId;
    private Guid _openStatusId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        // A 24/7 calendar keeps due-date arithmetic trivial (wall-clock) so these tests focus on the
        // clock STATE MACHINE, which the dedicated calculator suite already covers separately.
        var calendar = new BusinessCalendar { Name = new LocalizedText("Always", "دائم"), TimeZoneId = "UTC", IsTwentyFourSeven = true };
        _db.BusinessCalendars.Add(calendar);

        var normalPriority = new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي"), Level = 1 };
        var urgentPriority = new TicketPriority { Code = "urgent", Name = new LocalizedText("Urgent", "عاجل"), Level = 3 };
        _db.TicketPriorities.AddRange(normalPriority, urgentPriority);

        var newStatus = new TicketStatus { Code = "new", Name = new LocalizedText("New", "جديد"), Kind = TicketStatusKind.New, IsDefault = true };
        var openStatus = new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوح"), Kind = TicketStatusKind.Open };
        var pendingStatus = new TicketStatus { Code = "pending", Name = new LocalizedText("Pending", "بانتظار"), Kind = TicketStatusKind.Pending, PausesSla = true };
        _db.TicketStatuses.AddRange(newStatus, openStatus, pendingStatus);

        var category = new TicketCategory { Code = "general", Name = new LocalizedText("General", "عام") };
        _db.TicketCategories.Add(category);

        var policy = new SlaPolicy
        {
            Name = new LocalizedText("Standard", "قياسي"),
            BusinessCalendarId = calendar.Id,
            IsDefault = true,
            EvaluationOrder = 100,
            WarningThresholdPercent = 80,
        };
        policy.Targets.Add(new SlaTarget { PriorityId = normalPriority.Id, FirstResponseMinutes = 60, ResolutionMinutes = 480 });
        _db.SlaPolicies.Add(policy);

        await _db.SaveChangesAsync();

        _calendarId = calendar.Id;
        _normalPriorityId = normalPriority.Id;
        _pendingStatusId = pendingStatus.Id;
        _openStatusId = openStatus.Id;

        _now = new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => _now);

        _engine = new SlaEngine(
            _db,
            new BusinessCalendarCalculator(_db, new MemoryCache(new MemoryCacheOptions())),
            new ConditionEvaluator(NullLogger<ConditionEvaluator>.Instance),
            clock);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Ticket> CreateTicketAsync()
    {
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("Test", "اختبار"), PreferredLanguage = "en" };
        _db.Customers.Add(customer);

        var ticket = new Ticket
        {
            Number = "TCK-1",
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = _db.TicketCategories.First().Id,
            PriorityId = _normalPriorityId,
            StatusId = _db.TicketStatuses.First(s => s.Kind == TicketStatusKind.New).Id,
        };
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task ApplyPolicy_CreatesBothClocksWithCorrectDueDates()
    {
        var ticket = await CreateTicketAsync();

        await _engine.ApplyPolicyAsync(ticket.Id);

        var clocks = _db.TicketSlaClocks.Where(c => c.TicketId == ticket.Id).ToList();
        clocks.Should().HaveCount(2);
        clocks.Single(c => c.TargetType == SlaTargetType.FirstResponse).DueAt.Should().Be(_now.AddMinutes(60));
        clocks.Single(c => c.TargetType == SlaTargetType.Resolution).DueAt.Should().Be(_now.AddMinutes(480));

        var reloaded = _db.Tickets.Single(t => t.Id == ticket.Id);
        reloaded.FirstResponseDueAt.Should().Be(_now.AddMinutes(60));
        reloaded.ResolutionDueAt.Should().Be(_now.AddMinutes(480));
    }

    [Fact]
    public async Task ApplyPolicy_AppliedTwice_UpdatesExistingClocksRatherThanDuplicating()
    {
        var ticket = await CreateTicketAsync();

        await _engine.ApplyPolicyAsync(ticket.Id);
        await _engine.ApplyPolicyAsync(ticket.Id);

        _db.TicketSlaClocks.Count(c => c.TicketId == ticket.Id).Should().Be(2);
    }

    [Fact]
    public async Task TicketWithNoMatchingTarget_GetsNoClocks()
    {
        var ticket = await CreateTicketAsync();
        var uncoveredPriority = new TicketPriority { Code = "low", Name = new LocalizedText("Low", "منخفض"), Level = 0 };
        _db.TicketPriorities.Add(uncoveredPriority);
        ticket.PriorityId = uncoveredPriority.Id;
        await _db.SaveChangesAsync();

        await _engine.ApplyPolicyAsync(ticket.Id);

        _db.TicketSlaClocks.Count(c => c.TicketId == ticket.Id).Should().Be(0);
        _db.Tickets.Single(t => t.Id == ticket.Id).ResolutionDueAt.Should().BeNull();
    }

    [Fact]
    public async Task FirstReply_OnTime_MarksClockMet()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id);

        _now = _now.AddMinutes(30); // within the 60-minute target
        await _engine.OnFirstAgentReplyAsync(ticket.Id);

        var firstResponseClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.FirstResponse);
        firstResponseClock.Status.Should().Be(SlaClockStatus.Met);
        firstResponseClock.MetAt.Should().Be(_now);
    }

    [Fact]
    public async Task FirstReply_Late_MarksClockBreached_NotMet()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id);

        _now = _now.AddMinutes(90); // past the 60-minute target
        await _engine.OnFirstAgentReplyAsync(ticket.Id);

        var firstResponseClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.FirstResponse);
        firstResponseClock.Status.Should().Be(SlaClockStatus.Breached);
        _db.Tickets.Single(t => t.Id == ticket.Id).IsFirstResponseBreached.Should().BeTrue();
    }

    [Fact]
    public async Task MovingToPendingCustomer_PausesResolutionClockOnly()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id);

        ticket.StatusId = _pendingStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id);

        var resolutionClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.Resolution);
        var firstResponseClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.FirstResponse);

        resolutionClock.Status.Should().Be(SlaClockStatus.Paused);
        resolutionClock.PausedAt.Should().Be(_now);
        firstResponseClock.Status.Should().Be(SlaClockStatus.Running, "pausing must never affect the first-response commitment");
    }

    [Fact]
    public async Task ReturningFromPending_ResumesFromRemainingBudget_NotOriginalTarget()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id); // resolution due in 480 minutes

        ticket.StatusId = _pendingStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id); // pause after 0 elapsed minutes

        _now = _now.AddMinutes(1000); // a long time waiting on the customer — must not count against the budget
        ticket.StatusId = _openStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id); // resume

        var resolutionClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.Resolution);
        resolutionClock.Status.Should().Be(SlaClockStatus.Running);
        // Elapsed was ~0 when paused, so the full 480-minute budget remains from "now".
        resolutionClock.DueAt.Should().Be(_now.AddMinutes(480));
    }

    [Fact]
    public async Task PausedTwice_AccumulatesPausedMinutesAcrossBothCycles()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id);

        ticket.StatusId = _pendingStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id);

        _now = _now.AddMinutes(100);
        ticket.StatusId = _openStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id);

        _now = _now.AddMinutes(50);
        ticket.StatusId = _pendingStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id);

        _now = _now.AddMinutes(200);
        ticket.StatusId = _openStatusId;
        await _db.SaveChangesAsync();
        await _engine.OnStatusChangedAsync(ticket.Id);

        var resolutionClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.Resolution);
        resolutionClock.PausedMinutes.Should().Be(300); // 100 + 200 across the two pause cycles
    }

    [Fact]
    public async Task Resolve_OnTime_MarksResolutionClockMet()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id);

        _now = _now.AddMinutes(100); // within the 480-minute resolution target
        await _engine.OnResolvedAsync(ticket.Id);

        var resolutionClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.Resolution);
        resolutionClock.Status.Should().Be(SlaClockStatus.Met);
    }

    [Fact]
    public async Task Resolve_Late_MarksResolutionClockBreached()
    {
        var ticket = await CreateTicketAsync();
        await _engine.ApplyPolicyAsync(ticket.Id);

        _now = _now.AddMinutes(500); // past the 480-minute resolution target
        await _engine.OnResolvedAsync(ticket.Id);

        var resolutionClock = _db.TicketSlaClocks.Single(c => c.TicketId == ticket.Id && c.TargetType == SlaTargetType.Resolution);
        resolutionClock.Status.Should().Be(SlaClockStatus.Breached);
        _db.Tickets.Single(t => t.Id == ticket.Id).IsResolutionBreached.Should().BeTrue();
    }
}
