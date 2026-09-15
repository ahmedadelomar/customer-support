using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Customers.Queries;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Application.Customers;

/// <summary>
/// Covers <see cref="GetInteractionsQueryHandler"/> — in particular the keyset pagination, since
/// this is the story's explicit reason for not using offset paging (verification step 5 in the
/// story plan: a row must never be skipped or duplicated when a new entry lands mid-scroll).
/// Nothing in the product can generate interactions yet (ticket creation and the channel pipeline
/// are future stories), so this test seeds the timeline directly — the same way this query will
/// eventually be exercised once those stories call <c>IInteractionRecorder</c>.
/// </summary>
public class GetInteractionsQueryHandlerTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Customer NewCustomer() => new()
    {
        Code = $"CUS-{Random.Shared.Next(100000, 999999)}",
        DisplayName = new LocalizedText("Test Customer", "عميل تجريبي"),
        PreferredLanguage = "en",
    };

    private static Interaction NewInteraction(
        Guid customerId, DateTimeOffset occurredAt, ChannelKey channel = ChannelKey.Email,
        MessageDirection direction = MessageDirection.Inbound, Guid? ticketId = null, Guid? agentId = null) => new()
    {
        CustomerId = customerId,
        Channel = channel,
        Direction = direction,
        OccurredAt = occurredAt,
        TicketId = ticketId,
        AgentId = agentId,
    };

    private static IUserDisplayNameResolver NoAgents()
    {
        var resolver = Substitute.For<IUserDisplayNameResolver>();
        resolver.ResolveAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LocalizedText>());
        return resolver;
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        await using var db = CreateContext();
        var handler = new GetInteractionsQueryHandler(db, NoAgents());

        var act = () => handler.Handle(new GetInteractionsQuery { CustomerId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReturnsNewestFirst()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        db.Customers.Add(customer);

        var t0 = DateTimeOffset.UtcNow;
        db.Interactions.AddRange(
            NewInteraction(customer.Id, t0.AddMinutes(-2)),
            NewInteraction(customer.Id, t0),
            NewInteraction(customer.Id, t0.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var handler = new GetInteractionsQueryHandler(db, NoAgents());
        var result = await handler.Handle(new GetInteractionsQuery { CustomerId = customer.Id }, CancellationToken.None);

        result.Items.Should().BeInDescendingOrder(i => i.OccurredAt);
    }

    [Fact]
    public async Task Handle_SetsHasMore_WhenMoreRowsExistThanPageSize()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        db.Customers.Add(customer);

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            db.Interactions.Add(NewInteraction(customer.Id, now.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var handler = new GetInteractionsQueryHandler(db, NoAgents());
        var result = await handler.Handle(
            new GetInteractionsQuery { CustomerId = customer.Id, PageSize = 3 }, CancellationToken.None);

        result.Items.Should().HaveCount(3, "the extra probe row must be dropped from the payload");
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LastPage_ReportsHasMoreFalse()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        db.Customers.Add(customer);

        var now = DateTimeOffset.UtcNow;
        db.Interactions.AddRange(
            NewInteraction(customer.Id, now),
            NewInteraction(customer.Id, now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var handler = new GetInteractionsQueryHandler(db, NoAgents());
        var result = await handler.Handle(
            new GetInteractionsQuery { CustomerId = customer.Id, PageSize = 10 }, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
    }

    /// <summary>
    /// The behaviour offset paging gets wrong: a page fetched with a keyset cursor from an earlier
    /// call must never repeat or skip a row, even though a brand-new interaction was inserted
    /// between the two calls at a timestamp that would sort ahead of the cursor. This is exactly
    /// verification step 5 in the story plan.
    /// </summary>
    [Fact]
    public async Task Handle_KeysetCursor_NeverRepeatsOrSkipsRows_EvenWhenANewRowArrivesBetweenPages()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        db.Customers.Add(customer);

        var now = DateTimeOffset.UtcNow;
        // Oldest to newest: -5, -4, -3, -2, -1 minutes.
        var seeded = Enumerable.Range(1, 5)
            .Select(i => NewInteraction(customer.Id, now.AddMinutes(-i)))
            .ToList();
        db.Interactions.AddRange(seeded);
        await db.SaveChangesAsync();

        var handler = new GetInteractionsQueryHandler(db, NoAgents());

        // Page 1: newest two (-1, -2).
        var page1 = await handler.Handle(
            new GetInteractionsQuery { CustomerId = customer.Id, PageSize = 2 }, CancellationToken.None);
        page1.Items.Should().HaveCount(2);
        var cursor = page1.Items[^1]; // the -2 minute row

        // Simulate a new interaction landing "in the past" relative to now, but after page 1 was
        // read — offset paging would shift every subsequent page by one and either repeat or skip
        // a row; keyset paging must not, because it only asks for rows strictly older than the cursor.
        db.Interactions.Add(NewInteraction(customer.Id, now.AddSeconds(-30)));
        await db.SaveChangesAsync();

        var page2 = await handler.Handle(
            new GetInteractionsQuery
            {
                CustomerId = customer.Id,
                PageSize = 2,
                Before = cursor.OccurredAt,
                BeforeId = cursor.Id,
            },
            CancellationToken.None);

        page2.Items.Should().HaveCount(2);
        page2.Items.Select(i => i.Id).Should().NotContain(page1.Items.Select(i => i.Id),
            "no row already returned on page 1 may reappear on page 2");
        page2.Items.Should().OnlyContain(i => i.OccurredAt < cursor.OccurredAt,
            "every row on page 2 must be strictly older than the last row of page 1");
    }

    [Fact]
    public async Task Handle_FiltersByChannelAndDirectionAndDateRange()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        db.Customers.Add(customer);

        var now = DateTimeOffset.UtcNow;
        db.Interactions.AddRange(
            NewInteraction(customer.Id, now, ChannelKey.Email, MessageDirection.Inbound),
            NewInteraction(customer.Id, now.AddMinutes(-1), ChannelKey.WhatsApp, MessageDirection.Outbound),
            NewInteraction(customer.Id, now.AddDays(-10), ChannelKey.Email, MessageDirection.Inbound));
        await db.SaveChangesAsync();

        var handler = new GetInteractionsQueryHandler(db, NoAgents());

        var byChannel = await handler.Handle(
            new GetInteractionsQuery { CustomerId = customer.Id, Channel = ChannelKey.Email }, CancellationToken.None);
        byChannel.Items.Should().HaveCount(2).And.OnlyContain(i => i.Channel == ChannelKey.Email);

        var byDirection = await handler.Handle(
            new GetInteractionsQuery { CustomerId = customer.Id, Direction = MessageDirection.Outbound },
            CancellationToken.None);
        byDirection.Items.Should().HaveCount(1).And.OnlyContain(i => i.Direction == MessageDirection.Outbound);

        var byDateRange = await handler.Handle(
            new GetInteractionsQuery { CustomerId = customer.Id, From = now.AddDays(-1) }, CancellationToken.None);
        byDateRange.Items.Should().HaveCount(2, "the 10-day-old row falls outside the range");
    }

    [Fact]
    public async Task Handle_ResolvesTicketNumber_ForInteractionsLinkedToATicket()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        var category = new TicketCategory { Code = "general", Name = new LocalizedText("General", "عام") };
        var priority = new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي") };
        var status = new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوح") };
        db.AddRange(customer, category, priority, status);

        var ticket = new Ticket
        {
            Number = "TCK-2026-000042",
            CustomerId = customer.Id,
            Customer = customer,
            Subject = "Test",
            Description = "Test",
            Category = category,
            Priority = priority,
            Status = status,
        };
        db.Tickets.Add(ticket);
        db.Interactions.Add(NewInteraction(customer.Id, DateTimeOffset.UtcNow, ticketId: ticket.Id));
        await db.SaveChangesAsync();

        var handler = new GetInteractionsQueryHandler(db, NoAgents());
        var result = await handler.Handle(new GetInteractionsQuery { CustomerId = customer.Id }, CancellationToken.None);

        result.Items.Single().TicketNumber.Should().Be("TCK-2026-000042");
    }

    [Fact]
    public async Task Handle_ResolvesAgentDisplayName_ForInteractionsWithAnAgent()
    {
        await using var db = CreateContext();
        var customer = NewCustomer();
        db.Customers.Add(customer);
        var agentId = Guid.NewGuid();
        db.Interactions.Add(NewInteraction(customer.Id, DateTimeOffset.UtcNow, agentId: agentId));
        await db.SaveChangesAsync();

        var resolver = Substitute.For<IUserDisplayNameResolver>();
        resolver.ResolveAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LocalizedText> { [agentId] = new("Agent One", "الوكيل الأول") });

        var handler = new GetInteractionsQueryHandler(db, resolver);
        var result = await handler.Handle(new GetInteractionsQuery { CustomerId = customer.Id }, CancellationToken.None);

        result.Items.Single().AgentNameEn.Should().Be("Agent One");
        result.Items.Single().AgentNameAr.Should().Be("الوكيل الأول");
    }
}
