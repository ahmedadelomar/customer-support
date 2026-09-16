namespace CustomerSupport.Application.Channels.WebForms;

public record WebFormDefinitionDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string TitleEn { get; init; } = string.Empty;
    public string TitleAr { get; init; } = string.Empty;
    public string DescriptionEn { get; init; } = string.Empty;
    public string DescriptionAr { get; init; } = string.Empty;
    public string SubmitButtonLabelEn { get; init; } = string.Empty;
    public string SubmitButtonLabelAr { get; init; } = string.Empty;
    public string ThankYouMessageEn { get; init; } = string.Empty;
    public string ThankYouMessageAr { get; init; } = string.Empty;
    public IReadOnlyList<WebFormField> Fields { get; init; } = [];
    public Guid? DefaultCategoryId { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public Guid? DefaultDepartmentId { get; init; }
    public bool RequireCaptcha { get; init; }
    public int RateLimitPerHour { get; init; }
    public bool IsActive { get; init; }
    public int SubmissionCount { get; init; }
}

public record WebFormDefinitionRequest
{
    public string Key { get; init; } = string.Empty;
    public string TitleEn { get; init; } = string.Empty;
    public string TitleAr { get; init; } = string.Empty;
    public string DescriptionEn { get; init; } = string.Empty;
    public string DescriptionAr { get; init; } = string.Empty;
    public string SubmitButtonLabelEn { get; init; } = string.Empty;
    public string SubmitButtonLabelAr { get; init; } = string.Empty;
    public string ThankYouMessageEn { get; init; } = string.Empty;
    public string ThankYouMessageAr { get; init; } = string.Empty;
    public IReadOnlyList<WebFormField> Fields { get; init; } = [];
    public Guid? DefaultCategoryId { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public Guid? DefaultDepartmentId { get; init; }
    public bool RequireCaptcha { get; init; } = true;
    public int RateLimitPerHour { get; init; } = 10;
    public bool IsActive { get; init; } = true;
}

/// <summary>What the public renderer fetches — no captcha secret, no internal ids the visitor has no use for.</summary>
public record PublicWebFormSchemaDto(
    string Key,
    string TitleEn,
    string TitleAr,
    string DescriptionEn,
    string DescriptionAr,
    string SubmitButtonLabelEn,
    string SubmitButtonLabelAr,
    IReadOnlyList<WebFormField> Fields,
    bool RequireCaptcha);

public record SubmitWebFormRequest(Dictionary<string, string> Values, string? CaptchaToken);

public record SubmitWebFormResult(Guid SubmissionId, Guid? TicketId, string? TicketNumber, string ThankYouMessageEn, string ThankYouMessageAr);

public record WebFormSubmissionDto(
    Guid Id,
    Guid WebFormDefinitionId,
    string? SubmitterName,
    string? SubmitterEmail,
    string? SubmitterPhone,
    string Status,
    string? FailureReason,
    Guid? TicketId,
    string? TicketNumber,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ProcessedAt,
    string PayloadJson);
