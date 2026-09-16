using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Security;

/// <summary>
/// Branch scoping is opt-in per query — deliberately, because background jobs and cross-branch
/// reports need the whole table. The cost of opt-in is that a handler can silently forget it, so
/// these tests seed two branches and assert the behaviour rather than inspecting the implementation:
/// a query that scopes the wrong table would still pass a source-level check.
/// </summary>
public class BranchScopingTests
{
    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>A user restricted to one branch. An EMPTY accessible set means unrestricted, not "none".</summary>
    private static ICurrentUser UserIn(Guid? branchId, params Guid[] accessible)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(Guid.NewGuid());
        user.BranchId.Returns(branchId);
        user.AccessibleBranchIds.Returns(accessible);
        user.DepartmentIds.Returns(Array.Empty<Guid>());
        user.IsAuthenticated.Returns(true);
        user.HasPermission(Arg.Any<string>()).Returns(true);
        return user;
    }

    private static Customer CustomerIn(Guid? branchId, string name) => new()
    {
        Code = $"CUS-{Random.Shared.Next(100000, 999999)}",
        DisplayName = new LocalizedText(name, name),
        PreferredLanguage = "en",
        BranchId = branchId,
    };

    private static async Task<AppDbContext> SeedTwoBranchesAsync()
    {
        var db = CreateContext();

        db.Branches.AddRange(
            new Branch { Id = BranchA, Code = "A", Name = new LocalizedText("Branch A", "الفرع أ") },
            new Branch { Id = BranchB, Code = "B", Name = new LocalizedText("Branch B", "الفرع ب") });

        db.Customers.AddRange(
            CustomerIn(BranchA, "Customer A"),
            CustomerIn(BranchB, "Customer B"),
            CustomerIn(null, "Global Customer"));

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task RestrictedUser_SeesOnlyTheirOwnBranchPlusGlobalRecords()
    {
        await using var db = await SeedTwoBranchesAsync();

        var visible = await db.Customers
            .WhereBranchAccessible(UserIn(BranchA, BranchA))
            .Select(c => c.DisplayName.En)
            .ToListAsync();

        visible.Should().BeEquivalentTo(["Customer A", "Global Customer"],
            "a null BranchId means global and is visible from every branch");
    }

    [Fact]
    public async Task UnrestrictedUser_SeesEveryBranch()
    {
        await using var db = await SeedTwoBranchesAsync();

        // Empty accessible set — the head-office case.
        var visible = await db.Customers
            .WhereBranchAccessible(UserIn(BranchA))
            .Select(c => c.DisplayName.En)
            .ToListAsync();

        visible.Should().HaveCount(3);
    }

    [Fact]
    public async Task MultiBranchUser_SeesEveryBranchTheyWereGranted()
    {
        await using var db = await SeedTwoBranchesAsync();

        var visible = await db.Customers
            .WhereBranchAccessible(UserIn(BranchA, BranchA, BranchB))
            .Select(c => c.DisplayName.En)
            .ToListAsync();

        visible.Should().HaveCount(3);
    }

    /// <summary>
    /// The filter must be applied BEFORE the id match, so an out-of-scope record is indistinguishable
    /// from one that does not exist. Returning 403 instead would confirm the record exists.
    /// </summary>
    [Fact]
    public async Task OutOfScopeRecord_IsIndistinguishableFromMissing()
    {
        await using var db = await SeedTwoBranchesAsync();

        var otherBranchCustomerId = await db.Customers
            .Where(c => c.BranchId == BranchB)
            .Select(c => c.Id)
            .FirstAsync();

        var found = await db.Customers
            .WhereBranchAccessible(UserIn(BranchA, BranchA))
            .FirstOrDefaultAsync(c => c.Id == otherBranchCustomerId);

        found.Should().BeNull("the handler then throws NotFound, which is a 404 rather than a 403");
    }

    /// <summary>
    /// Background jobs must NOT scope: the auto-close sweep and the SLA breach sweep run for the whole
    /// system, and silently scoping them to one branch would leave other branches unprocessed forever.
    /// </summary>
    [Fact]
    public async Task UnscopedQuery_StillSeesEveryBranch()
    {
        await using var db = await SeedTwoBranchesAsync();

        var all = await db.Customers.CountAsync();

        all.Should().Be(3);
    }

    [Fact]
    public async Task TicketVisibility_WithoutViewAll_IsLimitedToOwnDepartmentOrOwnAssignments()
    {
        await using var db = CreateContext();

        var department = Guid.NewGuid();
        var otherDepartment = Guid.NewGuid();
        var me = Guid.NewGuid();

        var customer = CustomerIn(null, "Customer");
        var category = new TicketCategory { Code = "gen", Name = new LocalizedText("General", "عام") };
        var priority = new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي") };
        var status = new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوحة") };
        db.AddRange(customer, category, priority, status);

        Ticket NewTicket(string number, Guid? departmentId, Guid? assignee) => new()
        {
            Number = number,
            Subject = number,
            CustomerId = customer.Id,
            CategoryId = category.Id,
            PriorityId = priority.Id,
            StatusId = status.Id,
            DepartmentId = departmentId,
            AssignedAgentId = assignee,
        };

        db.Tickets.AddRange(
            NewTicket("MINE-DEPT", department, null),
            NewTicket("ASSIGNED-TO-ME", otherDepartment, me),
            NewTicket("SOMEONE-ELSE", otherDepartment, Guid.NewGuid()));

        await db.SaveChangesAsync();

        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(me);
        user.DepartmentIds.Returns([department]);
        user.HasPermission(Permissions.Tickets.ViewAll).Returns(false);

        var visible = await db.Tickets.WhereTicketVisible(user).Select(t => t.Number).ToListAsync();

        visible.Should().BeEquivalentTo(["MINE-DEPT", "ASSIGNED-TO-ME"]);
    }

    [Fact]
    public async Task TicketVisibility_WithViewAll_LiftsDepartmentScoping()
    {
        await using var db = CreateContext();

        var customer = CustomerIn(null, "Customer");
        var category = new TicketCategory { Code = "gen", Name = new LocalizedText("General", "عام") };
        var priority = new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي") };
        var status = new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوحة") };
        db.AddRange(customer, category, priority, status);

        db.Tickets.Add(new Ticket
        {
            Number = "OTHER-DEPT",
            Subject = "Other",
            CustomerId = customer.Id,
            CategoryId = category.Id,
            PriorityId = priority.Id,
            StatusId = status.Id,
            DepartmentId = Guid.NewGuid(),
        });

        await db.SaveChangesAsync();

        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(Guid.NewGuid());
        user.DepartmentIds.Returns(Array.Empty<Guid>());
        user.HasPermission(Permissions.Tickets.ViewAll).Returns(true);

        var visible = await db.Tickets.WhereTicketVisible(user).CountAsync();

        visible.Should().Be(1);
    }
}
