using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;

namespace CustomerSupport.Domain.Tickets;

/// <summary>
/// The central aggregate of the CRM (Ticket Management / Create and track tickets).
/// Every mutation must also append a <see cref="TicketEvent"/> so the history stays complete.
/// </summary>
public class Ticket : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Public reference shown to customers (<c>TCK-2026-000123</c>). Unique and immutable.</summary>
    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>The specific contact that raised the ticket, when known.</summary>
    public Guid? CustomerContactId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>BCP-47 tag of the language for this ticket; drives reply templates and notifications.</summary>
    public string Language { get; set; } = "ar";

    public Guid CategoryId { get; set; }
    public TicketCategory Category { get; set; } = null!;
    public Guid PriorityId { get; set; }
    public TicketPriority Priority { get; set; } = null!;
    public Guid StatusId { get; set; }
    public TicketStatus Status { get; set; } = null!;

    public ChannelKey Channel { get; set; }
    public Guid? ChannelAccountId { get; set; }

    public Guid? DepartmentId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }

    // --- SLA: denormalised from TicketSlaClock for list filtering and sorting ---
    public Guid? SlaPolicyId { get; set; }
    public DateTimeOffset? FirstResponseDueAt { get; set; }
    public DateTimeOffset? ResolutionDueAt { get; set; }
    public DateTimeOffset? FirstRespondedAt { get; set; }
    public bool IsFirstResponseBreached { get; set; }
    public bool IsResolutionBreached { get; set; }

    // --- Lifecycle ---
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? LastCustomerReplyAt { get; set; }
    public DateTimeOffset? LastAgentReplyAt { get; set; }

    /// <summary>Zero means not escalated. Raised by escalation rules, reset when a new level owner takes over.</summary>
    public int EscalationLevel { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public int ReopenCount { get; set; }
    public int CustomerReplyCount { get; set; }

    /// <summary>Set when this ticket was merged into another; the target becomes the surviving ticket.</summary>
    public Guid? MergedIntoTicketId { get; set; }

    /// <summary>Confidence reported by the AI categorisation job, so agents can judge the suggestion.</summary>
    public decimal? AiCategoryConfidence { get; set; }

    /// <summary>Latest AI summary, refreshed as the conversation grows. Full history lives in <c>AiSuggestion</c>.</summary>
    public string? AiSummary { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketEvent> Events { get; set; } = new List<TicketEvent>();
    public ICollection<TicketTag> Tags { get; set; } = new List<TicketTag>();
    public ICollection<TicketWatcher> Watchers { get; set; } = new List<TicketWatcher>();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
