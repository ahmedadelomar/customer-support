using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Domain.Channels;

/// <summary>
/// Per-attempt delivery record for an outbound message. Provider webhooks update the status,
/// which is what lets agents see whether a reply actually reached the customer.
/// </summary>
public class MessageDeliveryLog : BaseEntity
{
    public Guid TicketMessageId { get; set; }
    public TicketMessage TicketMessage { get; set; } = null!;

    public Guid? ChannelAccountId { get; set; }
    /// <summary>Address the message was sent to, kept even if the contact row later changes.</summary>
    public string Recipient { get; set; } = string.Empty;

    public MessageDeliveryStatus Status { get; set; }
    /// <summary>Provider-side id used to correlate later webhook callbacks.</summary>
    public string? ProviderMessageId { get; set; }
    public string? ProviderName { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public int Attempt { get; set; } = 1;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>SMS only (CS-304) — how many provider-billed parts the message split into.</summary>
    public int? SegmentCount { get; set; }
    /// <summary>SMS only — <see cref="SegmentCount"/> times the configured per-segment cost, a reporting estimate rather than the provider's actual invoice.</summary>
    public decimal? EstimatedCost { get; set; }
}
