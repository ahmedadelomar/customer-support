namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>One quick reply, as the composer picker and the management screen both show it.</summary>
public record QuickReplyDto
{
    public Guid Id { get; init; }
    public string Scope { get; init; } = "Personal";
    public Guid? OwnerId { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamNameEn { get; init; }
    public string? TeamNameAr { get; init; }
    public string? Shortcut { get; init; }
    public string TitleEn { get; init; } = string.Empty;
    public string TitleAr { get; init; } = string.Empty;
    public string BodyEn { get; init; } = string.Empty;
    public string BodyAr { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? CategoryNameEn { get; init; }
    public string? CategoryNameAr { get; init; }
    public Guid? ChannelId { get; init; }
    public string? ChannelNameEn { get; init; }
    public string? ChannelNameAr { get; init; }
    public int UsageCount { get; init; }
    public bool IsActive { get; init; } = true;
    /// <summary>Whether the caller may edit/delete this row — manage permission, plus the global permission for Global scope.</summary>
    public bool CanEdit { get; init; }
}
