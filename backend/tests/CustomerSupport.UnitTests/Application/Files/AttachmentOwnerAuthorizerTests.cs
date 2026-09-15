using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Files;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Application.Files;

/// <summary>
/// Covers the property this class exists to guarantee: an unrecognised owner type is refused, not
/// silently allowed, and access to a note is scoped through its customer's branch even though the
/// note itself carries no BranchId.
/// </summary>
public class AttachmentOwnerAuthorizerTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUser CurrentUser(
        Guid? branchId = null, IReadOnlyCollection<Guid>? accessibleBranches = null, params string[] permissions)
    {
        var user = Substitute.For<ICurrentUser>();
        user.BranchId.Returns(branchId);
        user.AccessibleBranchIds.Returns(accessibleBranches ?? []);
        user.HasPermission(Arg.Any<string>()).Returns(ci => permissions.Contains((string)ci[0]));
        return user;
    }

    [Fact]
    public async Task CanAccessAsync_WithAnUnrecognisedOwnerType_IsRefused()
    {
        await using var db = CreateContext();
        var authorizer = new AttachmentOwnerAuthorizer(
            db, CurrentUser(null, null, "customers.view", "tickets.view", "kb.view", "customers.notes.view"));

        // A typo, or a future aggregate someone forgot to add a branch for — either way, refused.
        var result = await authorizer.CanAccessAsync("SomeNewAggregateNobodyWiredUp", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_Customer_RequiresThePermission()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var withoutPermission = new AttachmentOwnerAuthorizer(db, CurrentUser());
        (await withoutPermission.CanAccessAsync("Customer", customer.Id)).Should().BeFalse();

        var withPermission = new AttachmentOwnerAuthorizer(db, CurrentUser(permissions: "customers.view"));
        (await withPermission.CanAccessAsync("Customer", customer.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_Customer_RespectsBranchScope()
    {
        await using var db = CreateContext();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var customer = new Customer { Code = "CUS-2", DisplayName = new LocalizedText("Test", "تجربة"), BranchId = branchA };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var scopedToOtherBranch = new AttachmentOwnerAuthorizer(
            db, CurrentUser(accessibleBranches: [branchB], permissions: "customers.view"));
        (await scopedToOtherBranch.CanAccessAsync("Customer", customer.Id)).Should().BeFalse();

        var scopedToCorrectBranch = new AttachmentOwnerAuthorizer(
            db, CurrentUser(accessibleBranches: [branchA], permissions: "customers.view"));
        (await scopedToCorrectBranch.CanAccessAsync("Customer", customer.Id)).Should().BeTrue();

        // Empty AccessibleBranchIds means unrestricted (system administrator) — the established
        // convention throughout this codebase's WhereBranchAccessible usage.
        var unrestricted = new AttachmentOwnerAuthorizer(db, CurrentUser(permissions: "customers.view"));
        (await unrestricted.CanAccessAsync("Customer", customer.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_CustomerNote_ScopesThroughItsCustomersBranch_EvenThoughTheNoteHasNoBranchIdOfItsOwn()
    {
        await using var db = CreateContext();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var customer = new Customer { Code = "CUS-3", DisplayName = new LocalizedText("Test", "تجربة"), BranchId = branchA };
        db.Customers.Add(customer);
        var note = new CustomerNote { CustomerId = customer.Id, Body = "note" };
        db.CustomerNotes.Add(note);
        await db.SaveChangesAsync();

        var wrongBranch = new AttachmentOwnerAuthorizer(
            db, CurrentUser(accessibleBranches: [branchB], permissions: "customers.notes.view"));
        (await wrongBranch.CanAccessAsync("CustomerNote", note.Id)).Should().BeFalse();

        var correctBranch = new AttachmentOwnerAuthorizer(
            db, CurrentUser(accessibleBranches: [branchA], permissions: "customers.notes.view"));
        (await correctBranch.CanAccessAsync("CustomerNote", note.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_CustomerNote_ForAnUnknownNoteId_IsRefused()
    {
        await using var db = CreateContext();
        var authorizer = new AttachmentOwnerAuthorizer(db, CurrentUser(permissions: "customers.notes.view"));

        (await authorizer.CanAccessAsync("CustomerNote", Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_CustomerNote_ForASoftDeletedNote_IsRefused()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-4", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);
        var note = new CustomerNote { CustomerId = customer.Id, Body = "note", IsDeleted = true };
        db.CustomerNotes.Add(note);
        await db.SaveChangesAsync();

        var authorizer = new AttachmentOwnerAuthorizer(db, CurrentUser(permissions: "customers.notes.view"));

        (await authorizer.CanAccessAsync("CustomerNote", note.Id)).Should().BeFalse();
    }
}
