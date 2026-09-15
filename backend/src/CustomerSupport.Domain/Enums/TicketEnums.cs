namespace CustomerSupport.Domain.Enums;

/// <summary>Coarse bucket every configurable <c>TicketStatus</c> row maps onto; drives SLA clocks and reporting.</summary>
public enum TicketStatusKind
{
    New = 0,
    Open = 1,
    Pending = 2,
    OnHold = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6,
}

/// <summary>Where a ticket originated. Mirrors the Communication Channels section.</summary>
public enum ChannelKey
{
    Email = 0,
    WhatsApp = 1,
    LiveChat = 2,
    Sms = 3,
    WebForm = 4,
    Portal = 5,
    Phone = 6,
    Api = 7,
    Internal = 8,
}

public enum MessageDirection
{
    Inbound = 0,
    Outbound = 1,
}

public enum MessageAuthorType
{
    Customer = 0,
    Agent = 1,
    System = 2,
    Bot = 3,
}

/// <summary>Delivery lifecycle reported back by the email/SMS/WhatsApp providers.</summary>
public enum MessageDeliveryStatus
{
    Queued = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4,
    Bounced = 5,
}

/// <summary>Every mutation appended to the ticket timeline (Ticket history).</summary>
public enum TicketEventType
{
    Created = 0,
    StatusChanged = 1,
    PriorityChanged = 2,
    CategoryChanged = 3,
    Assigned = 4,
    Unassigned = 5,
    Escalated = 6,
    MessageAdded = 7,
    InternalNoteAdded = 8,
    AttachmentAdded = 9,
    SlaBreached = 10,
    Merged = 11,
    Reopened = 12,
    Resolved = 13,
    Closed = 14,
    DepartmentChanged = 15,
    TagsChanged = 16,
    WatcherAdded = 17,
    /// <summary>Recorded on the closed/cancelled ticket when a customer reply creates a linked follow-up instead of reopening it.</summary>
    FollowUpCreated = 18,
    WatcherRemoved = 19,
}
