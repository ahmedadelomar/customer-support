namespace CustomerSupport.Domain.Enums;

/// <summary>Knowledge Base content types (FAQs, Help articles, Solutions and guides).</summary>
public enum KbArticleType
{
    Faq = 0,
    Article = 1,
    Solution = 2,
    Guide = 3,
}

public enum PublicationStatus
{
    Draft = 0,
    InReview = 1,
    Published = 2,
    Archived = 3,
}

/// <summary>Which AI capability produced an <c>AiSuggestion</c>.</summary>
public enum AiSuggestionType
{
    TicketSummary = 0,
    SuggestedReply = 1,
    Categorization = 2,
    SuggestedSolution = 3,
    SentimentAnalysis = 4,
}

/// <summary>Human review outcome for an AI suggestion — the accept/edit/reject rate is a reported KPI.</summary>
public enum AiSuggestionStatus
{
    Pending = 0,
    Accepted = 1,
    Edited = 2,
    Rejected = 3,
    Expired = 4,
}

public enum ChatbotOutcome
{
    InProgress = 0,
    ResolvedByBot = 1,
    EscalatedToAgent = 2,
    AbandonedByCustomer = 3,
}

public enum ChatMessageRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3,
}
