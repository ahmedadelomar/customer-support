using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// A live-chat conversation (Communication Channels / Live chat). Starts anonymous, is linked to a
/// customer once identified, and may be promoted to a ticket when it needs follow-up.
/// </summary>
public class ChatSession : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid? ChannelAccountId { get; set; }

    /// <summary>Browser-scoped anonymous id issued by the widget before the visitor identifies.</summary>
    public string VisitorKey { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? TicketId { get; set; }

    public string? VisitorName { get; set; }
    public string? VisitorEmail { get; set; }
    public string Language { get; set; } = "ar";
    /// <summary>Page the widget was opened from, useful context for the agent.</summary>
    public string? PageUrl { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public Guid? AssignedAgentId { get; set; }
    public Guid? QueuedForTeamId { get; set; }
    /// <summary>Waiting, Bot, Active, Ended or Abandoned.</summary>
    public string Status { get; set; } = "Waiting";

    /// <summary>True while the AI chatbot is handling the conversation before any handover.</summary>
    public bool IsBotHandled { get; set; }
    public DateTimeOffset? HandedOverAt { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FirstAgentReplyAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int MessageCount { get; set; }
    /// <summary>Post-chat rating, 1 to 5, when the visitor answers the survey.</summary>
    public int? Rating { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
