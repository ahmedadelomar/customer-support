using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Application.Tickets.Commands;
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
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Tickets;

/// <summary>
/// The invariant "every mutation appends an event" (Ticket Management / Ticket history, CS-205) is
/// only real if it is tested. One fact per ticket command, run against the real
/// <see cref="TicketEventRecorder"/> — not a substitute — so this proves the actual write path, not
/// just that a fake was called. <c>Transfer</c> (department transfer, CS-1203's
/// <see cref="TicketEventType.DepartmentChanged"/>) is deliberately not covered: no command writes it
/// yet, so it is marked skipped below rather than silently omitted.
/// </summary>
public class TicketHistoryCompletenessTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IDateTimeProvider FixedClock(DateTimeOffset? now = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now ?? DateTimeOffset.UtcNow);
        return clock;
    }

    /// <summary>Sees every branch and every ticket — keeps each test focused on the event it is checking.</summary>
    private static ICurrentUser AdminUser(Guid? userId = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId ?? Guid.NewGuid());
        user.UserName.Returns("Test Agent");
        user.BranchId.Returns((Guid?)null);
        user.AccessibleBranchIds.Returns(Array.Empty<Guid>());
        user.DepartmentIds.Returns(Array.Empty<Guid>());
        user.CustomerId.Returns((Guid?)null);
        user.IsAuthenticated.Returns(true);
        user.HasPermission(Arg.Any<string>()).Returns(true);
        return user;
    }

    private static Customer NewCustomer() => new()
    {
        Code = $"CUS-{Random.Shared.Next(100000, 999999)}",
        DisplayName = new LocalizedText("Test Customer", "عميل تجريبي"),
        PreferredLanguage = "en",
    };

    private static TicketCategory NewCategory(string code) => new()
    {
        Code = code,
        Name = new LocalizedText($"Category {code}", $"تصنيف {code}"),
    };

    private static TicketPriority NewPriority(string code, bool isDefault = false) => new()
    {
        Code = code,
        Name = new LocalizedText($"Priority {code}", $"أولوية {code}"),
        IsDefault = isDefault,
    };

    private static TicketStatus NewStatus(
        string code, TicketStatusKind kind = TicketStatusKind.Open, bool isTerminal = false, bool isDefault = false) => new()
    {
        Code = code,
        Name = new LocalizedText($"Status {code}", $"حالة {code}"),
        Kind = kind,
        IsTerminal = isTerminal,
        IsDefault = isDefault,
    };

    private static Ticket NewTicket(
        Customer customer, TicketCategory category, TicketPriority priority, TicketStatus status) => new()
    {
        Number = $"TCK-2026-{Random.Shared.Next(100000, 999999)}",
        CustomerId = customer.Id,
        Subject = "Test ticket",
        Description = "Test description",
        Language = "en",
        CategoryId = category.Id,
        PriorityId = priority.Id,
        StatusId = status.Id,
    };

    private static IAgentDirectory AgentDirectoryReturning(Guid agentId, string nameEn = "Agent One", string nameAr = "الوكيل الأول")
    {
        var directory = Substitute.For<IAgentDirectory>();
        var snapshot = new AgentSnapshot(agentId, new LocalizedText(nameEn, nameAr), true, "Available", 20, null);
        directory.GetAsync(agentId, Arg.Any<CancellationToken>()).Returns(snapshot);
        return directory;
    }

    private static TicketEvent LastEventOfType(AppDbContext db, Guid ticketId, TicketEventType type) =>
        db.TicketEvents.Where(e => e.TicketId == ticketId && e.EventType == type)
            .OrderByDescending(e => e.OccurredAt).First();

    [Fact]
    public async Task CreateTicket_RecordsCreatedEvent()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal", isDefault: true);
        var status = NewStatus("new", TicketStatusKind.New, isDefault: true);
        db.AddRange(customer, category, priority, status);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new CreateTicketCommandHandler(
            db, currentUser,
            FakeReferenceNumbers(), new TicketEventRecorder(db, currentUser, FixedClock()),
            Substitute.For<IInteractionRecorder>(), FixedClock());

        var ticketId = await handler.Handle(
            new CreateTicketCommand { CustomerId = customer.Id, Subject = "Help", Description = "Need help", CategoryId = category.Id },
            CancellationToken.None);

        var evt = LastEventOfType(db, ticketId, TicketEventType.Created);
        evt.IsSystemGenerated.Should().BeFalse();
        evt.ActorId.Should().Be(currentUser.UserId);
    }

    [Fact]
    public async Task UpdateTicket_ChangingCategoryAndPriority_RecordsBothEvents_WithDisplayValues()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var oldCategory = NewCategory("general");
        var newCategory = NewCategory("billing");
        var oldPriority = NewPriority("normal");
        var newPriority = NewPriority("urgent");
        var status = NewStatus("open");
        db.AddRange(customer, oldCategory, newCategory, oldPriority, newPriority, status);
        var ticket = NewTicket(customer, oldCategory, oldPriority, status);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new UpdateTicketCommandHandler(db, new TicketEventRecorder(db, currentUser, FixedClock()), FixedClock());

        await handler.Handle(
            new UpdateTicketCommand
            {
                Id = ticket.Id, Subject = "Updated", Description = "Updated body",
                CategoryId = newCategory.Id, PriorityId = newPriority.Id,
            },
            CancellationToken.None);

        var categoryEvent = LastEventOfType(db, ticket.Id, TicketEventType.CategoryChanged);
        categoryEvent.OldDisplayValue.Should().Be(oldCategory.Name.En);
        categoryEvent.NewDisplayValue.Should().Be(newCategory.Name.En);

        var priorityEvent = LastEventOfType(db, ticket.Id, TicketEventType.PriorityChanged);
        priorityEvent.OldDisplayValue.Should().Be(oldPriority.Name.En);
        priorityEvent.NewDisplayValue.Should().Be(newPriority.Name.En);
    }

    [Fact]
    public async Task ChangeStatus_ToResolved_RecordsStatusChangedEvent_WithDisplayValues()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var openStatus = NewStatus("open", TicketStatusKind.Open);
        var resolvedStatus = NewStatus("resolved", TicketStatusKind.Resolved);
        db.AddRange(customer, category, priority, openStatus, resolvedStatus);
        var ticket = NewTicket(customer, category, priority, openStatus);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var sla = Substitute.For<ISlaEngine>();
        var handler = new ChangeTicketStatusCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()), sla, FixedClock());

        await handler.Handle(
            new ChangeTicketStatusCommand { TicketId = ticket.Id, StatusId = resolvedStatus.Id, ResolutionNote = "Fixed it" },
            CancellationToken.None);

        var evt = LastEventOfType(db, ticket.Id, TicketEventType.StatusChanged);
        evt.OldDisplayValue.Should().Be(openStatus.Name.En);
        evt.NewDisplayValue.Should().Be(resolvedStatus.Name.En);
    }

    [Fact]
    public async Task Assign_RecordsAssignedEvent_WithTheNewAgentsDisplayName()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var status = NewStatus("open");
        db.AddRange(customer, category, priority, status);
        var ticket = NewTicket(customer, category, priority, status);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var agentId = Guid.NewGuid();
        var currentUser = AdminUser();
        var capacity = Substitute.For<IAgentCapacityService>();
        capacity.CheckAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new CapacityResult(true, false, 0, null, null));

        var handler = new AssignTicketCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()), capacity,
            AgentDirectoryReturning(agentId), Substitute.For<INotificationDispatcher>(), FixedClock());

        await handler.Handle(new AssignTicketCommand { TicketId = ticket.Id, AgentId = agentId }, CancellationToken.None);

        var evt = LastEventOfType(db, ticket.Id, TicketEventType.Assigned);
        evt.NewDisplayValue.Should().Be("Agent One");
    }

    [Fact]
    public async Task Unassign_RecordsUnassignedEvent_WithThePreviousAgentsDisplayName()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var status = NewStatus("open");
        db.AddRange(customer, category, priority, status);
        var agentId = Guid.NewGuid();
        var ticket = NewTicket(customer, category, priority, status);
        ticket.AssignedAgentId = agentId;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new UnassignTicketCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()),
            AgentDirectoryReturning(agentId), Substitute.For<INotificationDispatcher>());

        await handler.Handle(new UnassignTicketCommand(ticket.Id), CancellationToken.None);

        var evt = LastEventOfType(db, ticket.Id, TicketEventType.Unassigned);
        evt.OldDisplayValue.Should().Be("Agent One");
    }

    [Fact]
    public async Task Escalate_RecordsEscalatedEvent_WithTheReasonInMetadata()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var status = NewStatus("open");
        db.AddRange(customer, category, priority, status);
        var ticket = NewTicket(customer, category, priority, status);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new EscalateTicketCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()),
            Substitute.For<INotificationDispatcher>(), FixedClock());

        await handler.Handle(
            new EscalateTicketCommand { TicketId = ticket.Id, Reason = "Customer is very upset" },
            CancellationToken.None);

        var evt = LastEventOfType(db, ticket.Id, TicketEventType.Escalated);
        evt.NewValue.Should().Be("1");
        evt.MetadataJson.Should().Contain("Customer is very upset");
    }

    [Fact]
    public async Task Reply_RecordsMessageAddedEvent()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var status = NewStatus("open");
        db.AddRange(customer, category, priority, status);
        var ticket = NewTicket(customer, category, priority, status);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new ReplyToTicketCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()),
            Substitute.For<IInteractionRecorder>(), Substitute.For<ISlaEngine>(),
            Substitute.For<INotificationDispatcher>(), FixedClock());

        await handler.Handle(
            new ReplyToTicketCommand { TicketId = ticket.Id, BodyText = "Here is your answer" },
            CancellationToken.None);

        LastEventOfType(db, ticket.Id, TicketEventType.MessageAdded).Should().NotBeNull();
    }

    [Fact]
    public async Task AddInternalNote_RecordsInternalNoteAddedEvent()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var status = NewStatus("open");
        db.AddRange(customer, category, priority, status);
        var ticket = NewTicket(customer, category, priority, status);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new AddInternalNoteCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()),
            Substitute.For<INotificationDispatcher>(), FixedClock());

        await handler.Handle(
            new AddInternalNoteCommand { TicketId = ticket.Id, BodyText = "Escalated to billing team" },
            CancellationToken.None);

        LastEventOfType(db, ticket.Id, TicketEventType.InternalNoteAdded).Should().NotBeNull();
    }

    [Fact]
    public async Task Merge_RecordsMergedEvent_OnBothTheSourceAndTheTarget()
    {
        // MergeTicketsCommandHandler moves messages with ExecuteUpdateAsync, which the EF Core
        // InMemory provider cannot translate at all (even over zero rows) — a real, open Sqlite
        // in-memory connection is used here instead, exactly what the dev database itself runs on.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var openStatus = NewStatus("open");
        var terminalStatus = NewStatus("cancelled", TicketStatusKind.Cancelled, isTerminal: true);
        db.AddRange(customer, category, priority, openStatus, terminalStatus);
        var source = NewTicket(customer, category, priority, openStatus);
        var target = NewTicket(customer, category, priority, openStatus);
        db.Tickets.AddRange(source, target);
        await db.SaveChangesAsync();

        var currentUser = AdminUser();
        var handler = new MergeTicketsCommandHandler(db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()));

        await handler.Handle(
            new MergeTicketsCommand { TicketId = source.Id, TargetTicketId = target.Id }, CancellationToken.None);

        var sourceEvent = LastEventOfType(db, source.Id, TicketEventType.Merged);
        sourceEvent.NewDisplayValue.Should().Be(target.Number);

        var targetEvent = LastEventOfType(db, target.Id, TicketEventType.Merged);
        targetEvent.OldDisplayValue.Should().Be(source.Number);
    }

    /// <summary>
    /// Transfer is CS-1203's <c>DepartmentChanged</c> event. Also pins the two rules that make a
    /// transfer more than a reassignment: the assignee is cleared, and the SLA due dates are left
    /// exactly as they were — restarting the clock on an internal routing change would hide breaches.
    /// </summary>
    [Fact]
    public async Task Transfer_RecordsDepartmentChangedEvent()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = NewCategory("general");
        var priority = NewPriority("normal");
        var status = NewStatus("open");
        var fromDepartment = new Department { Code = "BILL", Name = new LocalizedText("Billing", "الفواتير") };
        var toDepartment = new Department { Code = "TECH", Name = new LocalizedText("Technical", "الدعم الفني") };

        db.AddRange(customer, category, priority, status, fromDepartment, toDepartment);

        var ticket = NewTicket(customer, category, priority, status);
        ticket.DepartmentId = fromDepartment.Id;
        ticket.AssignedAgentId = Guid.NewGuid();
        ticket.AssignedAt = DateTimeOffset.UtcNow;
        ticket.FirstResponseDueAt = DateTimeOffset.UtcNow.AddHours(4);
        ticket.ResolutionDueAt = DateTimeOffset.UtcNow.AddDays(2);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var firstResponseDueBefore = ticket.FirstResponseDueAt;
        var resolutionDueBefore = ticket.ResolutionDueAt;

        var currentUser = AdminUser();
        var handler = new TransferTicketCommandHandler(
            db, currentUser, new TicketEventRecorder(db, currentUser, FixedClock()),
            Substitute.For<INotificationDispatcher>());

        await handler.Handle(
            new TransferTicketCommand
            {
                TicketId = ticket.Id,
                DepartmentId = toDepartment.Id,
                Reason = "Needs a technical specialist",
            },
            CancellationToken.None);

        var evt = LastEventOfType(db, ticket.Id, TicketEventType.DepartmentChanged);
        evt.OldDisplayValue.Should().Be("Billing");
        evt.NewDisplayValue.Should().Be("Technical");
        evt.MetadataJson.Should().Contain("Needs a technical specialist");

        ticket.DepartmentId.Should().Be(toDepartment.Id);
        ticket.AssignedAgentId.Should().BeNull("the receiving department picks its own owner");
        ticket.FirstResponseDueAt.Should().Be(firstResponseDueBefore, "a transfer must not restart the SLA clock");
        ticket.ResolutionDueAt.Should().Be(resolutionDueBefore, "a transfer must not restart the SLA clock");
    }

    private static IReferenceNumberGenerator FakeReferenceNumbers()
    {
        var numbers = Substitute.For<IReferenceNumberGenerator>();
        numbers.NextTicketNumberAsync(Arg.Any<CancellationToken>())
            .Returns(_ => $"TCK-2026-{Random.Shared.Next(100000, 999999)}");
        return numbers;
    }
}
