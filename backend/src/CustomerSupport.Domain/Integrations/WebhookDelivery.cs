using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Integrations;

/// <summary>One delivery attempt of one event to one webhook, retained so failures can be replayed.</summary>
public class WebhookDelivery : BaseEntity
{
    public Guid WebhookId { get; set; }
    public Webhook Webhook { get; set; } = null!;

    public string EventType { get; set; } = string.Empty;
    /// <summary>Idempotency key the receiver can use to discard duplicates.</summary>
    public string EventId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";

    public int Attempt { get; set; } = 1;
    public int? ResponseStatusCode { get; set; }
    /// <summary>Truncated response body, kept short deliberately.</summary>
    public string? ResponseBody { get; set; }
    public string? Error { get; set; }
    public int DurationMs { get; set; }

    /// <summary>Pending, Delivered, Failed or Abandoned.</summary>
    public string Status { get; set; } = "Pending";
    /// <summary>Next exponential-backoff attempt time; null once delivered or abandoned.</summary>
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
