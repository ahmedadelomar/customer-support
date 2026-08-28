using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Reporting;

/// <summary>One tile on a dashboard, positioned on a 12-column responsive grid.</summary>
public class DashboardWidget : BaseEntity
{
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;

    public LocalizedText Title { get; set; } = new();
    /// <summary>stat, line, bar, pie, table, gauge or list.</summary>
    public string WidgetType { get; set; } = "stat";
    /// <summary>Report or metric the widget renders.</summary>
    public Guid? ReportDefinitionId { get; set; }
    public string? MetricKey { get; set; }

    /// <summary>Widget-specific settings as JSON: filters, thresholds, colour overrides.</summary>
    public string ConfigJson { get; set; } = "{}";

    // --- Grid position on a 12-column layout ---
    public int Column { get; set; }
    public int Row { get; set; }
    public int ColumnSpan { get; set; } = 3;
    public int RowSpan { get; set; } = 1;
}
