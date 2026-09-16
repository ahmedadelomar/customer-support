using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Infrastructure.Services;

/// <summary>
/// Rule evaluation, routing strategies and the decision log behind automatic assignment (CS-502).
/// </summary>
public class AssignmentEngineTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private AssignmentEngine _engine = null!;
    private Guid _departmentId;
    private Guid _teamId;
    private Guid _categoryId;
    private Guid _priorityId;
    private readonly List<Guid> _memberIds = [];

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var department = new Department { Name = new LocalizedText("Support", "الدعم") };
        _db.Departments.Add(department);

        var team = new Team { Department = department, Name = new LocalizedText("Team A", "الفريق أ") };
        _db.Teams.Add(team);

        for (var i = 0; i < 3; i++)
        {
            var userId = Guid.NewGuid();
            _memberIds.Add(userId);
            _db.TeamMembers.Add(new TeamMember { Team = team, UserId = userId, RotationOrder = i, IsActive = true, JoinedAt = DateTimeOffset.UtcNow });
        }

        var category = new TicketCategory { Code = "general", Name = new LocalizedText("General", "عام") };
        _db.TicketCategories.Add(category);
        var priority = new TicketPriority { Code = "high", Name = new LocalizedText("High", "عالي"), Level = 2 };
        _db.TicketPriorities.Add(priority);

        await _db.SaveChangesAsync();
        _departmentId = department.Id;
        _teamId = team.Id;
        _categoryId = category.Id;
        _priorityId = priority.Id;

        var agentDirectory = Substitute.For<IAgentDirectory>();
        agentDirectory.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new AgentSnapshot(ci.Arg<Guid>(), new LocalizedText("Agent", "وكيل"), true, "Available", 20, _departmentId));

        var capacity = Substitute.For<IAgentCapacityService>();
        capacity.CheckAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => new CapacityResult(true, false, 0, 20, null));

        _engine = new AssignmentEngine(
            _db,
            new RuleEvaluator(new ConditionEvaluator(NullLogger<ConditionEvaluator>.Instance), NullLogger<RuleEvaluator>.Instance),
            capacity,
            agentDirectory,
            new NoOpEventRecorder(),
            Substitute.For<INotificationDispatcher>(),
            Substitute.For<IDateTimeProvider>(),
            NullLogger<AssignmentEngine>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>A minimal real recorder — a substitute would need every overload stubbed to avoid nulls.</summary>
    private class NoOpEventRecorder : ITicketEventRecorder
    {
        public void Record(Guid ticketId, TicketEventType eventType, string? field = null, string? oldValue = null,
            string? newValue = null, string? oldDisplay = null, string? newDisplay = null, string? metadataJson = null,
            string? triggeredByRule = null)
        {
        }
    }

    private async Task<Ticket> CreateTicketAsync()
    {
        var customer = new Customer { Code = $"CUS-{Guid.NewGuid():N}", DisplayName = new LocalizedText("Test", "اختبار"), PreferredLanguage = "en" };
        _db.Customers.Add(customer);

        var ticket = new Ticket
        {
            Number = $"TCK-{Guid.NewGuid():N}",
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = _categoryId,
            PriorityId = _priorityId,
            StatusId = SeedDefaultStatus(),
            DepartmentId = _departmentId,
        };
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    private Guid SeedDefaultStatus()
    {
        var existing = _db.TicketStatuses.FirstOrDefault();
        if (existing is not null)
        {
            return existing.Id;
        }

        var status = new TicketStatus { Code = "new", Name = new LocalizedText("New", "جديد"), Kind = TicketStatusKind.New, IsDefault = true };
        _db.TicketStatuses.Add(status);
        _db.SaveChanges();
        return status.Id;
    }

    private void AddRoundRobinRule(bool respectAvailability = true, int evaluationOrder = 1)
    {
        _db.AssignmentRules.Add(new AssignmentRule
        {
            Name = new LocalizedText("Route to Team A", "توجيه إلى الفريق أ"),
            EvaluationOrder = evaluationOrder,
            IsActive = true,
            ConditionsJson = "[]",
            Strategy = AssignmentStrategy.RoundRobin,
            TargetTeamId = _teamId,
            RespectAgentAvailability = respectAvailability,
            StopProcessing = true,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task RoundRobin_CyclesThroughMembersInRotationOrder()
    {
        AddRoundRobinRule();

        var first = await _engine.AssignAsync((await CreateTicketAsync()).Id);
        var second = await _engine.AssignAsync((await CreateTicketAsync()).Id);
        var third = await _engine.AssignAsync((await CreateTicketAsync()).Id);
        var fourth = await _engine.AssignAsync((await CreateTicketAsync()).Id); // wraps back to the first member

        first.Should().Be(_memberIds[0]);
        second.Should().Be(_memberIds[1]);
        third.Should().Be(_memberIds[2]);
        fourth.Should().Be(_memberIds[0]);
    }

    [Fact]
    public async Task RoundRobin_CursorPersists_SoANewEngineInstanceContinuesWhereItLeftOff()
    {
        AddRoundRobinRule();
        await _engine.AssignAsync((await CreateTicketAsync()).Id); // consumes member 0

        // A fresh engine instance simulates the API restarting between requests — the cursor lives
        // on the Team row, not in memory.
        var freshEngine = new AssignmentEngine(
            _db,
            new RuleEvaluator(new ConditionEvaluator(NullLogger<ConditionEvaluator>.Instance), NullLogger<RuleEvaluator>.Instance),
            Substitute.For<IAgentCapacityService>().Also(c => c.CheckAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new CapacityResult(true, false, 0, 20, null))),
            Substitute.For<IAgentDirectory>().Also(d => d.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci => new AgentSnapshot(ci.Arg<Guid>(), new LocalizedText("Agent", "وكيل"), true, "Available", 20, _departmentId))),
            new NoOpEventRecorder(),
            Substitute.For<INotificationDispatcher>(),
            Substitute.For<IDateTimeProvider>(),
            NullLogger<AssignmentEngine>.Instance);

        var next = await freshEngine.AssignAsync((await CreateTicketAsync()).Id);
        next.Should().Be(_memberIds[1]);
    }

    [Fact]
    public async Task EveryCandidateIneligible_LeavesTicketUnassigned_AndLogsWhy()
    {
        _db.AssignmentRules.Add(new AssignmentRule
        {
            Name = new LocalizedText("Route", "توجيه"),
            EvaluationOrder = 1,
            IsActive = true,
            ConditionsJson = "[]",
            Strategy = AssignmentStrategy.RoundRobin,
            TargetTeamId = _teamId,
            RespectAgentAvailability = true,
            StopProcessing = true,
        });
        await _db.SaveChangesAsync();

        var capacity = Substitute.For<IAgentCapacityService>();
        capacity.CheckAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new CapacityResult(false, false, 25, 20, "Agent is at capacity."));

        var engine = new AssignmentEngine(
            _db,
            new RuleEvaluator(new ConditionEvaluator(NullLogger<ConditionEvaluator>.Instance), NullLogger<RuleEvaluator>.Instance),
            capacity,
            Substitute.For<IAgentDirectory>(),
            new NoOpEventRecorder(),
            Substitute.For<INotificationDispatcher>(),
            Substitute.For<IDateTimeProvider>(),
            NullLogger<AssignmentEngine>.Instance);

        var ticket = await CreateTicketAsync();
        var result = await engine.AssignAsync(ticket.Id);

        result.Should().BeNull();
        var reloaded = _db.Tickets.AsNoTracking().Single(t => t.Id == ticket.Id);
        reloaded.AssignedAgentId.Should().BeNull();

        var log = _db.AutomationRunLogs.Single(l => l.TicketId == ticket.Id);
        log.Outcome.Should().Be("Matched");
        log.Reason.Should().Contain("eligible");
    }

    [Fact]
    public async Task UnknownConditionField_DoesNotMatch_AndIsLogged()
    {
        _db.AssignmentRules.Add(new AssignmentRule
        {
            Name = new LocalizedText("Bad rule", "قاعدة خاطئة"),
            EvaluationOrder = 1,
            IsActive = true,
            ConditionsJson = """[{"field":"NotARealField","operator":"Equals","value":"x"}]""",
            Strategy = AssignmentStrategy.RoundRobin,
            TargetTeamId = _teamId,
            StopProcessing = true,
        });
        await _db.SaveChangesAsync();

        var ticket = await CreateTicketAsync();
        var result = await _engine.AssignAsync(ticket.Id);

        result.Should().BeNull();
        _db.AutomationRunLogs.Single(l => l.TicketId == ticket.Id).Outcome.Should().Be("Skipped");
    }

    [Fact]
    public async Task EveryRuleEvaluated_IsLogged_IncludingSkippedOnes()
    {
        _db.AssignmentRules.Add(new AssignmentRule
        {
            Name = new LocalizedText("Never matches", "لا تتطابق أبدا"),
            EvaluationOrder = 1,
            IsActive = true,
            ConditionsJson = """[{"field":"Priority.Code","operator":"Equals","value":"urgent"}]""",
            Strategy = AssignmentStrategy.RoundRobin,
            TargetTeamId = _teamId,
            StopProcessing = false,
        });
        // A distinct, higher EvaluationOrder — not a same-value tie broken by Guid — guarantees this
        // one evaluates second regardless of how the two rows' randomly generated ids compare.
        AddRoundRobinRule(evaluationOrder: 2);

        var ticket = await CreateTicketAsync();
        await _engine.AssignAsync(ticket.Id);

        _db.AutomationRunLogs.Count(l => l.TicketId == ticket.Id).Should().Be(2);
        _db.AutomationRunLogs.Count(l => l.TicketId == ticket.Id && l.Outcome == "Skipped").Should().Be(1);
        _db.AutomationRunLogs.Count(l => l.TicketId == ticket.Id && l.Outcome == "Matched").Should().Be(1);
    }

    [Fact]
    public async Task RuleTargetingAnUnknownDepartmentOnly_LeavesTicketQueued_WhenNoTeamGiven()
    {
        _db.AssignmentRules.Add(new AssignmentRule
        {
            Name = new LocalizedText("Queue only", "قائمة انتظار فقط"),
            EvaluationOrder = 1,
            IsActive = true,
            ConditionsJson = "[]",
            Strategy = AssignmentStrategy.QueueOnly,
            StopProcessing = true,
        });
        await _db.SaveChangesAsync();

        var ticket = await CreateTicketAsync();
        var result = await _engine.AssignAsync(ticket.Id);

        result.Should().BeNull();
        _db.Tickets.AsNoTracking().Single(t => t.Id == ticket.Id).AssignedAgentId.Should().BeNull();
    }
}

/// <summary>Small NSubstitute chaining helper so a substitute can be configured inline where it's built.</summary>
internal static class SubstituteExtensions
{
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
