using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// A raw web-form submission, retained even if ticket creation fails so nothing is lost and the
/// operation can be retried.
/// </summary>
public class WebFormSubmission : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid WebFormDefinitionId { get; set; }
    public WebFormDefinition WebFormDefinition { get; set; } = null!;

    /// <summary>Submitted answers keyed by field key, exactly as received.</summary>
    public string PayloadJson { get; set; } = "{}";

    public Guid? CustomerId { get; set; }
    public Guid? TicketId { get; set; }
    public string? SubmitterName { get; set; }
    public string? SubmitterEmail { get; set; }
    public string? SubmitterPhone { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Received, Processed, Rejected or Failed.</summary>
    public string Status { get; set; } = "Received";
    public string? FailureReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
