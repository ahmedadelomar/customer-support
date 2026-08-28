using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// A concrete endpoint on a channel: a support mailbox, a WhatsApp business number, an SMS sender id,
/// or a live-chat widget. Routing decides which department and branch inbound traffic lands in.
/// </summary>
public class ChannelAccount : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>The externally visible identity: mailbox address, phone number, sender id or widget key.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Provider connection this account sends and receives through.</summary>
    public Guid? IntegrationConnectionId { get; set; }

    /// <summary>Department inbound traffic is routed to when no rule overrides it.</summary>
    public Guid? DefaultDepartmentId { get; set; }
    public Guid? DefaultCategoryId { get; set; }
    public Guid? DefaultPriorityId { get; set; }

    /// <summary>Signature or footer appended to outbound replies, bilingual.</summary>
    public LocalizedText Signature { get; set; } = new();
    /// <summary>Auto-acknowledgement sent on ticket creation; empty disables it.</summary>
    public LocalizedText AutoReplyBody { get; set; } = new();
    public bool SendAutoReply { get; set; }

    /// <summary>Channel-specific settings as JSON, for example IMAP folder or WhatsApp template namespace.</summary>
    public string? SettingsJson { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastPolledAt { get; set; }
    public string? LastPollError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
