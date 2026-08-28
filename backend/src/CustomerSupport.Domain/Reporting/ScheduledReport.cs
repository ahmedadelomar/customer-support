using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Reporting;

/// <summary>A recurring email delivery of a report, run by the scheduler.</summary>
public class ScheduledReport : BaseEntity, IAuditable, ITenantScoped
{
    public Guid? BranchId { get; set; }
    public Guid ReportDefinitionId { get; set; }
    public ReportDefinition ReportDefinition { get; set; } = null!;

    public LocalizedText Name { get; set; } = new();
    /// <summary>Quartz cron expression evaluated in <see cref="TimeZoneId"/>.</summary>
    public string CronExpression { get; set; } = "0 0 7 ? * MON";
    public string TimeZoneId { get; set; } = "Asia/Riyadh";

    /// <summary>Semicolon-separated recipient addresses.</summary>
    public string Recipients { get; set; } = string.Empty;
    /// <summary>Pdf, Xlsx or Csv.</summary>
    public string Format { get; set; } = "Xlsx";
    public string Language { get; set; } = "ar";
    /// <summary>Parameter overrides applied on top of the report defaults.</summary>
    public string? ParametersJson { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public string? LastRunError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
}
