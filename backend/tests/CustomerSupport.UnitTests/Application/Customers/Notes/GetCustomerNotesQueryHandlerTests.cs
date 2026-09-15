using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Customers.Notes;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Application.Customers.Notes;

public class GetCustomerNotesQueryHandlerTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IUserDisplayNameResolver NoAgents()
    {
        var resolver = Substitute.For<IUserDisplayNameResolver>();
        resolver.ResolveAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LocalizedText>());
        return resolver;
    }

    [Fact]
    public async Task Handle_OrdersPinnedNotesFirst_ThenNewestFirst()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);

        var now = DateTimeOffset.UtcNow;
        db.CustomerNotes.AddRange(
            new CustomerNote { CustomerId = customer.Id, Body = "old, unpinned", CreatedAt = now.AddDays(-2) },
            new CustomerNote { CustomerId = customer.Id, Body = "new, unpinned", CreatedAt = now },
            new CustomerNote { CustomerId = customer.Id, Body = "old, pinned", IsPinned = true, CreatedAt = now.AddDays(-5) });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        var handler = new GetCustomerNotesQueryHandler(db, currentUser, NoAgents());

        var result = await handler.Handle(
            new GetCustomerNotesQuery { CustomerId = customer.Id }, CancellationToken.None);

        result.Items.Select(n => n.Body).Should().ContainInOrder("old, pinned", "new, unpinned", "old, unpinned");
    }

    [Fact]
    public async Task Handle_CanEdit_IsTrueForTheAuthor_EvenWithoutTheManagePermission()
    {
        await using var db = CreateContext();
        var authorId = Guid.NewGuid();
        var customer = new Customer { Code = "CUS-2", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);
        db.CustomerNotes.Add(new CustomerNote { CustomerId = customer.Id, Body = "mine", CreatedById = authorId });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(authorId);
        currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var handler = new GetCustomerNotesQueryHandler(db, currentUser, NoAgents());
        var result = await handler.Handle(new GetCustomerNotesQuery { CustomerId = customer.Id }, CancellationToken.None);

        result.Items.Single().CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CanEdit_IsFalseForADifferentAgentWithoutTheManagePermission()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-3", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);
        db.CustomerNotes.Add(new CustomerNote { CustomerId = customer.Id, Body = "someone else's", CreatedById = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var handler = new GetCustomerNotesQueryHandler(db, currentUser, NoAgents());
        var result = await handler.Handle(new GetCustomerNotesQuery { CustomerId = customer.Id }, CancellationToken.None);

        result.Items.Single().CanEdit.Should().BeFalse();
    }
}
