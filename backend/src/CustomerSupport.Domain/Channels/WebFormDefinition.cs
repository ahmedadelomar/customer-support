using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// An administrator-defined public form (Communication Channels / Web forms). The field list is stored
/// as JSON so new forms need no schema change, and each submission opens a ticket.
/// </summary>
public class WebFormDefinition : BaseEntity, IAuditable, ISoftDeletable, ITenantScoped
{
    public Guid? BranchId { get; set; }

    /// <summary>URL-safe key used in the public endpoint and embed snippet.</summary>
    public string Key { get; set; } = string.Empty;
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public LocalizedText SubmitButtonLabel { get; set; } = new();
    public LocalizedText ThankYouMessage { get; set; } = new();

    /// <summary>JSON array of field descriptors: key, type, label (en/ar), required, options, validation.</summary>
    public string FieldsJson { get; set; } = "[]";

    public Guid? DefaultCategoryId { get; set; }
    public Guid? DefaultPriorityId { get; set; }
    public Guid? DefaultDepartmentId { get; set; }

    /// <summary>Requires a valid captcha token when true. Recommended for public forms.</summary>
    public bool RequireCaptcha { get; set; } = true;
    /// <summary>Maximum submissions per IP per hour; zero disables throttling.</summary>
    public int RateLimitPerHour { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public int SubmissionCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
