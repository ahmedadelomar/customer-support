using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Workspace;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Soft-deleting an entity that owns a <see cref="LocalizedText"/> (table-split into the same row)
/// used to fail: EF Core cascades the owned dependents to <c>EntityState.Deleted</c> alongside the
/// parent, and the interceptor only flipped the PARENT back to <c>Modified</c>. Left as Deleted, the
/// owned dependents' columns were nulled out by the shared UPDATE, tripping their NOT NULL
/// constraints — first caught live via <c>DELETE /api/QuickReplies/{id}</c> (CS-404), and confirmed to
/// already silently affect <see cref="Customer"/> deletion (CS-101) too. A real Sqlite connection is
/// required here (not the InMemory provider) since the bug only manifests as a NOT NULL constraint
/// violation at the relational layer.
/// </summary>
public class AuditableEntityInterceptorTests
{
    private static AppDbContext CreateContext(ICurrentUser currentUser)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditableEntityInterceptor(currentUser, FixedClock()))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static IDateTimeProvider FixedClock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private static ICurrentUser AdminUser()
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(Guid.NewGuid());
        return user;
    }

    [Fact]
    public async Task Remove_QuickReply_SoftDeletesWithoutNullingOwnedLocalizedText()
    {
        await using var db = CreateContext(AdminUser());

        var quickReply = new QuickReply
        {
            Scope = "Personal",
            OwnerId = Guid.NewGuid(),
            Title = new LocalizedText("Refund confirmation", "تأكيد الاسترداد"),
            Body = new LocalizedText("Your refund is confirmed.", "تم تأكيد الاسترداد الخاص بك."),
        };
        db.QuickReplies.Add(quickReply);
        await db.SaveChangesAsync();

        db.QuickReplies.Remove(quickReply);
        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        var reloaded = await db.QuickReplies.IgnoreQueryFilters().SingleAsync(q => q.Id == quickReply.Id);
        reloaded.IsDeleted.Should().BeTrue();
        reloaded.Title.En.Should().Be("Refund confirmation");
        reloaded.Body.Ar.Should().Be("تم تأكيد الاسترداد الخاص بك.");
    }

    [Fact]
    public async Task Remove_Customer_SoftDeletesWithoutNullingOwnedLocalizedText()
    {
        await using var db = CreateContext(AdminUser());

        var customer = new Customer
        {
            Code = $"CUS-{Random.Shared.Next(100000, 999999)}",
            DisplayName = new LocalizedText("Test Customer", "عميل تجريبي"),
            PreferredLanguage = "en",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Customers.Remove(customer);
        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        var reloaded = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customer.Id);
        reloaded.IsDeleted.Should().BeTrue();
        reloaded.DisplayName.En.Should().Be("Test Customer");
    }
}
