using CustomerSupport.Application.Common.Exceptions;
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

/// <summary>
/// Covers the "author or <c>customers.notes.manage</c>" rule in
/// <see cref="UpdateCustomerNoteCommandHandler"/> and <see cref="DeleteCustomerNoteCommandHandler"/>.
/// Not verifiable live yet: exercising the "different agent, no manage permission" branch needs a
/// second constrained user, and user/role management (CS-1001) isn't built beyond the bootstrap
/// administrator, who already holds every permission.
/// </summary>
public class NoteAuthorRuleTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUser CurrentUser(Guid userId, bool hasManagePermission)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId);
        user.HasPermission(Arg.Any<string>()).Returns(hasManagePermission);
        return user;
    }

    private static async Task<(AppDbContext Db, Customer Customer, CustomerNote Note, Guid AuthorId)> SeedAsync()
    {
        var db = CreateContext();
        var authorId = Guid.NewGuid();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("Test", "تجربة") };

        // Add() is what assigns the generated key — customer.Id is still Guid.Empty before this,
        // so the note must be constructed (reading customer.Id) only after this call.
        db.Customers.Add(customer);
        var note = new CustomerNote { CustomerId = customer.Id, Body = "original", CreatedById = authorId };
        db.CustomerNotes.Add(note);

        await db.SaveChangesAsync();
        return (db, customer, note, authorId);
    }

    [Fact]
    public async Task Update_ByTheAuthor_WithoutTheManagePermission_Succeeds()
    {
        var (db, customer, note, authorId) = await SeedAsync();
        var handler = new UpdateCustomerNoteCommandHandler(db, CurrentUser(authorId, hasManagePermission: false));

        await handler.Handle(
            new UpdateCustomerNoteCommand { CustomerId = customer.Id, NoteId = note.Id, Body = "edited by author" },
            CancellationToken.None);

        (await db.CustomerNotes.SingleAsync()).Body.Should().Be("edited by author");
    }

    [Fact]
    public async Task Update_ByADifferentAgent_WithoutTheManagePermission_IsForbidden()
    {
        var (db, customer, note, _) = await SeedAsync();
        var otherAgentId = Guid.NewGuid();
        var handler = new UpdateCustomerNoteCommandHandler(db, CurrentUser(otherAgentId, hasManagePermission: false));

        var act = () => handler.Handle(
            new UpdateCustomerNoteCommand { CustomerId = customer.Id, NoteId = note.Id, Body = "tampered" },
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await db.CustomerNotes.SingleAsync()).Body.Should().Be("original", "the forbidden edit must not have applied");
    }

    [Fact]
    public async Task Update_ByADifferentAgent_WithTheManagePermission_Succeeds()
    {
        var (db, customer, note, _) = await SeedAsync();
        var managerId = Guid.NewGuid();
        var handler = new UpdateCustomerNoteCommandHandler(db, CurrentUser(managerId, hasManagePermission: true));

        await handler.Handle(
            new UpdateCustomerNoteCommand { CustomerId = customer.Id, NoteId = note.Id, Body = "edited by manager" },
            CancellationToken.None);

        (await db.CustomerNotes.SingleAsync()).Body.Should().Be("edited by manager");
    }

    [Fact]
    public async Task Delete_ByADifferentAgent_WithoutTheManagePermission_IsForbidden()
    {
        var (db, customer, note, _) = await SeedAsync();
        var otherAgentId = Guid.NewGuid();
        var handler = new DeleteCustomerNoteCommandHandler(db, CurrentUser(otherAgentId, hasManagePermission: false));

        var act = () => handler.Handle(new DeleteCustomerNoteCommand(customer.Id, note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await db.CustomerNotes.CountAsync()).Should().Be(1, "the forbidden delete must not have applied");
    }

    [Fact]
    public async Task Delete_ByTheAuthor_Succeeds()
    {
        var (db, customer, note, authorId) = await SeedAsync();
        var handler = new DeleteCustomerNoteCommandHandler(db, CurrentUser(authorId, hasManagePermission: false));

        await handler.Handle(new DeleteCustomerNoteCommand(customer.Id, note.Id), CancellationToken.None);

        (await db.CustomerNotes.CountAsync()).Should().Be(0);
    }
}
