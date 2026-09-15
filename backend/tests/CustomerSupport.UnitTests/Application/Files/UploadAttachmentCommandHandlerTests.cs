using CustomerSupport.Application.Common.Exceptions;
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
/// Covers <see cref="UploadAttachmentCommandHandler"/>, in particular the infected-file path — no
/// real virus scanner exists in this codebase yet (only the no-op default), so nothing live can
/// ever return "infected" to exercise this. This is the only place that behaviour is checked.
/// </summary>
public class UploadAttachmentCommandHandlerTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IAttachmentOwnerAuthorizer AlwaysAllow()
    {
        var authorizer = Substitute.For<IAttachmentOwnerAuthorizer>();
        authorizer.CanAccessAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        return authorizer;
    }

    private static IAttachmentPolicyProvider Policy(long maxBytes = 10 * 1024 * 1024) =>
        new FixedPolicyProvider(new AttachmentPolicy(maxBytes, [".txt", ".pdf"]));

    private sealed class FixedPolicyProvider(AttachmentPolicy policy) : IAttachmentPolicyProvider
    {
        public Task<AttachmentPolicy> GetPolicyAsync(CancellationToken ct = default) => Task.FromResult(policy);
    }

    private static UploadAttachmentCommand NewCommand(Guid customerId, string fileName = "note.txt") => new()
    {
        OwnerType = "Customer",
        OwnerId = customerId,
        FileName = fileName,
        ContentType = "text/plain",
        SizeBytes = 5,
        Content = new MemoryStream("hello"u8.ToArray()),
    };

    [Fact]
    public async Task Handle_WhenTheScannerReportsInfected_DeletesTheSavedFile_AndNeverCreatesARow()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var storage = Substitute.For<IFileStorage>();
        storage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("2026/01/somekey.txt");

        var scanner = Substitute.For<IVirusScanner>();
        scanner.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("infected");

        var handler = new UploadAttachmentCommandHandler(db, AlwaysAllow(), Policy(), storage, scanner);

        var act = () => handler.Handle(NewCommand(customer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        await storage.Received(1).DeleteAsync("2026/01/somekey.txt", Arg.Any<CancellationToken>());
        (await db.Attachments.CountAsync()).Should().Be(0, "an infected file must never get an id that could be referenced or downloaded");
    }

    [Fact]
    public async Task Handle_WhenClean_CreatesTheAttachmentRowWithTheScanResultRecorded()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-2", DisplayName = new LocalizedText("Test", "تجربة") };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var storage = Substitute.For<IFileStorage>();
        storage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("2026/01/clean.txt");

        var scanner = Substitute.For<IVirusScanner>();
        scanner.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("skipped");

        var handler = new UploadAttachmentCommandHandler(db, AlwaysAllow(), Policy(), storage, scanner);
        var result = await handler.Handle(NewCommand(customer.Id), CancellationToken.None);

        var saved = await db.Attachments.SingleAsync();
        saved.Id.Should().Be(result.Id);
        saved.ScanResult.Should().Be("skipped");
        saved.StorageKey.Should().Be("2026/01/clean.txt");
    }

    [Fact]
    public async Task Handle_WithADisallowedExtension_ThrowsBeforeTouchingStorage()
    {
        await using var db = CreateContext();
        var storage = Substitute.For<IFileStorage>();

        var handler = new UploadAttachmentCommandHandler(
            db, AlwaysAllow(), Policy(), storage, Substitute.For<IVirusScanner>());

        var act = () => handler.Handle(NewCommand(Guid.NewGuid(), "malware.exe"), CancellationToken.None);

        await act.Should().ThrowAsync<CustomerSupport.Application.Common.Exceptions.ValidationException>();
        await storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OverTheSizeCap_ThrowsBeforeTouchingStorage()
    {
        await using var db = CreateContext();
        var storage = Substitute.For<IFileStorage>();
        var command = NewCommand(Guid.NewGuid()) with { SizeBytes = 20 * 1024 * 1024 };

        var handler = new UploadAttachmentCommandHandler(
            db, AlwaysAllow(), Policy(maxBytes: 10 * 1024 * 1024), storage, Substitute.For<IVirusScanner>());

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CustomerSupport.Application.Common.Exceptions.ValidationException>();
        await storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheAuthorizerRefuses_ThrowsForbidden_BeforeTouchingStorage()
    {
        await using var db = CreateContext();
        var storage = Substitute.For<IFileStorage>();
        var authorizer = Substitute.For<IAttachmentOwnerAuthorizer>();
        authorizer.CanAccessAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = new UploadAttachmentCommandHandler(db, authorizer, Policy(), storage, Substitute.For<IVirusScanner>());

        var act = () => handler.Handle(NewCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
