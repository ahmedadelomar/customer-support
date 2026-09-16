namespace CustomerSupport.Application.Channels.ChannelAccounts;

public record ChannelAccountDto
{
    public Guid Id { get; init; }
    public Guid ChannelId { get; init; }
    public int ChannelKey { get; init; }
    public string ChannelNameEn { get; init; } = string.Empty;
    public string ChannelNameAr { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public Guid? DefaultDepartmentId { get; init; }
    public Guid? DefaultCategoryId { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public string SignatureEn { get; init; } = string.Empty;
    public string SignatureAr { get; init; } = string.Empty;
    public string AutoReplyBodyEn { get; init; } = string.Empty;
    public string AutoReplyBodyAr { get; init; } = string.Empty;
    public bool SendAutoReply { get; init; }
    public string? SettingsJson { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? LastPolledAt { get; init; }
    public string? LastPollError { get; init; }
    /// <summary>True once a webhook secret has been set — the secret itself is never returned.</summary>
    public bool HasWebhookSecret { get; init; }
}

public record ChannelAccountRequest
{
    public Guid ChannelId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public Guid? DefaultDepartmentId { get; init; }
    public Guid? DefaultCategoryId { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public string SignatureEn { get; init; } = string.Empty;
    public string SignatureAr { get; init; } = string.Empty;
    public string AutoReplyBodyEn { get; init; } = string.Empty;
    public string AutoReplyBodyAr { get; init; } = string.Empty;
    public bool SendAutoReply { get; init; }
    public string? SettingsJson { get; init; }
    public bool IsActive { get; init; } = true;
    /// <summary>Write-only. Omitted (null) leaves the current secret unchanged; an empty string clears it.</summary>
    public string? WebhookSecret { get; init; }
}

public record TestChannelAccountConnectionResult(bool Success, string? Error);
