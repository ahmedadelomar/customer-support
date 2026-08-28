using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Ai;

/// <summary>
/// One turn in a chatbot conversation, retained in full so a handover gives the agent the whole
/// transcript and so prompt regressions can be replayed.
/// </summary>
public class ChatbotMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public ChatbotConversation Conversation { get; set; } = null!;

    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>JSON of tool or function calls the model issued, for example a knowledge-base lookup.</summary>
    public string? ToolCallsJson { get; set; }
    /// <summary>Knowledge-base articles retrieved for this turn.</summary>
    public string? RetrievedArticleIds { get; set; }

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? LatencyMs { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
