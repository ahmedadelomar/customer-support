using CustomerSupport.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Disk-backed <see cref="IFileStorage"/> for development and single-instance deployments. Swap for
/// a blob-storage implementation behind the same interface for a multi-instance deployment — no
/// caller changes, since every caller only ever sees an opaque storage key.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Attachments:RootPath"];

        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "attachments")
            : Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured);

        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        // The extension travels into the key only because the caller (UploadAttachmentCommand)
        // has already checked it against the allow-list — nothing else about fileName is trusted,
        // which is exactly why the key is a generated GUID rather than the name itself.
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var key = $"{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}/{Guid.NewGuid():N}{extension}";
        var full = ResolveWithinRoot(key);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);

        return key;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        var full = ResolveWithinRoot(storageKey);

        if (!File.Exists(full))
        {
            throw new FileNotFoundException("The stored file is missing.", storageKey);
        }

        Stream stream = File.OpenRead(full);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var full = ResolveWithinRoot(storageKey);

        if (File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a key to an absolute path and refuses anything that would land outside the
    /// configured root — defends a tampered key such as <c>../../appsettings.json</c>.
    /// <see cref="Path.GetFullPath(string)"/> alone is not enough: it happily collapses <c>..</c>
    /// segments into a path outside <see cref="_root"/> rather than rejecting them, so the check
    /// has to happen afterward, on the resolved path.
    /// </summary>
    private string ResolveWithinRoot(string storageKey)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storageKey));
        var relative = Path.GetRelativePath(_root, full);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new UnauthorizedAccessException("The storage key resolves outside the attachment root.");
        }

        return full;
    }
}
