using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Domain.Customers;

/// <summary>
/// One entry on the unified customer timeline (Customer Management / Interaction history).
/// Written by the channel ingestion pipeline, the ticket workflow and the portal — it is a
/// read-optimised projection, never edited by hand.
/// </summary>
public class Interaction : BaseEntity, ITenantScoped
{
    public Guid? BranchId { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? TicketId { get; set; }
    /// <summary>Points at the concrete row (<c>TicketMessage</c>, <c>ChatSession</c>, <c>CsatSurvey</c>, …).</summary>
    public Guid? SourceId { get; set; }
    public string? SourceType { get; set; }

    public ChannelKey Channel { get; set; }
    public MessageDirection Direction { get; set; }
    public string? Subject { get; set; }
    /// <summary>Short plain-text preview rendered on the timeline.</summary>
    public string? Preview { get; set; }
    public Guid? AgentId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
