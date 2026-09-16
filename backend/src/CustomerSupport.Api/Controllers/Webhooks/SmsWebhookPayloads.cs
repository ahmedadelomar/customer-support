namespace CustomerSupport.Api.Controllers.Webhooks;

public record InboundSmsWebhookPayload(string MessageId, string From, string? FromName, string Body, DateTimeOffset? SentAt);

public record SmsDeliveryStatusPayload(string ProviderMessageId, string Status, string? ErrorCode, string? ErrorMessage);
