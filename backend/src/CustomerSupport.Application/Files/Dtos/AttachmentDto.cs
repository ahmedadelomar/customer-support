namespace CustomerSupport.Application.Files.Dtos;

/// <summary>Metadata shown alongside an owning record — never the storage key.</summary>
public record AttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>The bytes and headers <see cref="DownloadAttachmentQuery"/> hands back to the controller.</summary>
public record AttachmentContent(Stream Content, string FileName, string ContentType);
