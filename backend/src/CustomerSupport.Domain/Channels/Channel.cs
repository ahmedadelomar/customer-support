using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// A supported communication channel (Communication Channels). One row per <see cref="ChannelKey"/>,
/// seeded at install time; administrators toggle availability rather than adding rows.
/// </summary>
public class Channel : BaseEntity, IAuditable
{
    public ChannelKey Key { get; set; }
    public LocalizedText Name { get; set; } = new();
    public string? Icon { get; set; }

    public bool IsEnabled { get; set; } = true;
    /// <summary>False for channels that can only receive, such as web forms.</summary>
    public bool SupportsOutbound { get; set; } = true;
    /// <summary>True when the channel can carry file attachments.</summary>
    public bool SupportsAttachments { get; set; } = true;
    /// <summary>Provider cap in bytes for a single attachment; null means use the global limit.</summary>
    public long? MaxAttachmentBytes { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<ChannelAccount> Accounts { get; set; } = new List<ChannelAccount>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
