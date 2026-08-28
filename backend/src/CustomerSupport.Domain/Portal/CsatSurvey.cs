using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Portal;

/// <summary>
/// A satisfaction survey issued when a ticket is resolved (Customer Portal / Submit feedback,
/// Reports / Customer satisfaction). The token lets a customer respond from an email or SMS link
/// without logging in.
/// </summary>
public class CsatSurvey : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public Guid TicketId { get; set; }
    public Guid CustomerId { get; set; }
    /// <summary>Agent credited with the response, captured at send time so later reassignment does not skew reports.</summary>
    public Guid? AgentId { get; set; }

    /// <summary>Single-use opaque token embedded in the survey link.</summary>
    public string Token { get; set; } = string.Empty;
    public string Language { get; set; } = "ar";

    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>Overall satisfaction, 1 to 5. Null until the customer responds.</summary>
    public int? Score { get; set; }
    /// <summary>Net Promoter style follow-up, 0 to 10, when enabled.</summary>
    public int? RecommendScore { get; set; }
    public string? Comment { get; set; }

    /// <summary>Set when a low score automatically opened a follow-up ticket.</summary>
    public Guid? FollowUpTicketId { get; set; }
    public int ReminderCount { get; set; }
}
