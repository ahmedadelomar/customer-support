using System.Text;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Services;

/// <summary>
/// Covers the path-traversal defence directly: this is exactly the property verification step 7 in
/// the story plan asks for ("a tampered id"), and it is not realistically triggerable through the
/// live API (nothing in the product ever lets a caller supply a storage key — this test is the only
/// place a regression here would be caught before it reached a real deployment).
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "cs-attachment-tests", Guid.NewGuid().ToString("N"));

    private LocalFileStorage CreateStorage()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Attachments:RootPath"].Returns(_tempRoot);

        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(Path.GetTempPath());

        return new LocalFileStorage(configuration, environment);
    }

    [Fact]
    public async Task SaveAsync_ThenOpenAsync_RoundTripsTheExactBytes()
    {
        var storage = CreateStorage();
        var original = Encoding.UTF8.GetBytes("hello world");

        using var input = new MemoryStream(original);
        var key = await storage.SaveAsync(input, "note.txt", "text/plain", CancellationToken.None);

        await using var opened = await storage.OpenAsync(key, CancellationToken.None);
        using var reader = new MemoryStream();
        await opened.CopyToAsync(reader);

        reader.ToArray().Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task SaveAsync_NeverUsesTheUploadedFileNameAsTheKey()
    {
        // The generated key must not be predictable from — or contain — the original filename,
        // since the filename is attacker-controlled and the key is what grants file-system access.
        var storage = CreateStorage();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("x"));

        var key = await storage.SaveAsync(input, "../../secret-plan.txt", "text/plain", CancellationToken.None);

        key.Should().NotContain("secret-plan");
        key.Should().EndWith(".txt"); // the extension alone is preserved, taken from the allow-listed value
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\Windows\\win.ini")]
    [InlineData("2026/01/../../../../secrets.txt")]
    public async Task OpenAsync_WithATraversalKey_ThrowsRatherThanEscapingTheRoot(string maliciousKey)
    {
        var storage = CreateStorage();

        var act = async () => await storage.OpenAsync(maliciousKey, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\Windows\\win.ini")]
    public async Task DeleteAsync_WithATraversalKey_ThrowsRatherThanDeletingOutsideTheRoot(string maliciousKey)
    {
        var storage = CreateStorage();

        var act = async () => await storage.DeleteAsync(maliciousKey, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task OpenAsync_ForAMissingFile_ThrowsFileNotFound_NotAnUnhandledException()
    {
        var storage = CreateStorage();

        var act = async () => await storage.OpenAsync("2026/01/does-not-exist.txt", CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
