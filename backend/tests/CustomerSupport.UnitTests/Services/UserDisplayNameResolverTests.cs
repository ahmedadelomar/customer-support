using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupport.UnitTests.Services;

/// <summary>
/// Exercises the exact failure mode already hit once in this codebase: a query projecting an owned
/// type (<see cref="LocalizedText"/>, here <c>ApplicationUser.DisplayName</c>) throws at
/// materialisation if run as a tracking query without its owner included. This resolver must use
/// <c>AsNoTracking()</c> — see <see cref="UserDisplayNameResolver"/>.
/// </summary>
public class UserDisplayNameResolverTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ResolveAsync_ReturnsDisplayNamesKeyedById_WithoutThrowing()
    {
        await using var db = CreateContext();

        var agent = new ApplicationUser
        {
            UserName = "agent1",
            DisplayName = new LocalizedText("Agent One", "الوكيل الأول"),
        };
        db.Users.Add(agent);
        await db.SaveChangesAsync();

        var resolver = new UserDisplayNameResolver(db);
        var result = await resolver.ResolveAsync([agent.Id]);

        result.Should().ContainKey(agent.Id);
        result[agent.Id].En.Should().Be("Agent One");
        result[agent.Id].Ar.Should().Be("الوكيل الأول");
    }

    [Fact]
    public async Task ResolveAsync_WithUnknownId_OmitsIt_RatherThanThrowing()
    {
        await using var db = CreateContext();
        var resolver = new UserDisplayNameResolver(db);

        var result = await resolver.ResolveAsync([Guid.NewGuid()]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WithNoIds_ReturnsEmptyWithoutQueryingTheDatabase()
    {
        await using var db = CreateContext();
        var resolver = new UserDisplayNameResolver(db);

        var result = await resolver.ResolveAsync([]);

        result.Should().BeEmpty();
    }
}
