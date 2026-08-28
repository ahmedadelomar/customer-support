using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Ai;

/// <summary>
/// A conversation handled by the AI chatbot (AI Features / AI chatbot). Tracks whether the bot
/// resolved the question or handed over, which is the deflection metric management reports on.
/// </summary>
public class ChatbotConversation : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }

    /// <summary>Set when the bot is fronting a live-chat widget session.</summary>
    public Guid? ChatSessionId { get; set; }
    public Guid? CustomerId { get; set; }
    /// <summary>Created only if the conversation was escalated or the customer asked to open a ticket.</summary>
    public Guid? TicketId { get; set; }

    public ChannelKey Channel { get; set; } = ChannelKey.LiveChat;
    public string Language { get; set; } = "ar";
    public ChatbotOutcome Outcome { get; set; } = ChatbotOutcome.InProgress;

    /// <summary>Agent the conversation was handed to, when it escalated.</summary>
    public Guid? HandedOverToAgentId { get; set; }
    public DateTimeOffset? HandedOverAt { get; set; }
    /// <summary>Why the bot gave up: low confidence, explicit request, or repeated failure.</summary>
    public string? HandoverReason { get; set; }

    public int MessageCount { get; set; }
    /// <summary>Article ids the bot cited, so content owners can see what actually answers questions.</summary>
    public string? CitedArticleIds { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    /// <summary>Post-conversation rating, 1 to 5.</summary>
    public int? Rating { get; set; }

    public ICollection<ChatbotMessage> Messages { get; set; } = new List<ChatbotMessage>();
}
