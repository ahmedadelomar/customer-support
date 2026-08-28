using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Reporting;

/// <summary>
/// A saved report: which built-in query to run and with what default filters. System rows ship with
/// the product; users may save their own variants.
/// </summary>
public class ReportDefinition : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    /// <summary>Identifies the server-side query handler, for example <c>tickets.volume</c>.</summary>
    public string Key { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public LocalizedText Description { get; set; } = new();

    /// <summary>One of Tickets, Sla, AgentPerformance, Satisfaction or Custom.</summary>
    public string Category { get; set; } = "Tickets";
    /// <summary>Default filter values as JSON: date range, department, channel and so on.</summary>
    public string ParametersJson { get; set; } = "{}";
    /// <summary>Preferred visualisation: table, line, bar, pie or stat.</summary>
    public string DefaultVisualization { get; set; } = "table";

    /// <summary>System reports cannot be deleted or renamed, only copied.</summary>
    public bool IsSystem { get; set; }
    public Guid? OwnerId { get; set; }
    /// <summary>Shared reports are visible to everyone holding the reports permission.</summary>
    public bool IsShared { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
