using CustomerSupport.Application.Files.Dtos;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Tickets.Dtos;

/// <summary>
/// Row shape for the ticket list. Carries both languages for every lookup plus the customer name and
/// SLA due times, so the grid never needs a second request to render.
/// </summary>
public record TicketListItemDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;

    public Guid CustomerId { get; init; }
    public string CustomerDisplayNameEn { get; init; } = string.Empty;
    public string CustomerDisplayNameAr { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }
    public string CategoryNameEn { get; init; } = string.Empty;
    public string CategoryNameAr { get; init; } = string.Empty;

    public Guid PriorityId { get; init; }
    public string PriorityNameEn { get; init; } = string.Empty;
    public string PriorityNameAr { get; init; } = string.Empty;
    public string PriorityColorHex { get; init; } = string.Empty;

    public Guid StatusId { get; init; }
    public string StatusNameEn { get; init; } = string.Empty;
    public string StatusNameAr { get; init; } = string.Empty;
    public string StatusColorHex { get; init; } = string.Empty;
    public TicketStatusKind StatusKind { get; init; }

    public ChannelKey Channel { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? AssignedAgentId { get; init; }
    public string? AssignedAgentNameEn { get; init; }
    public string? AssignedAgentNameAr { get; init; }

    public DateTimeOffset? FirstResponseDueAt { get; init; }
    public DateTimeOffset? ResolutionDueAt { get; init; }
    public bool IsFirstResponseBreached { get; init; }
    public bool IsResolutionBreached { get; init; }

    public DateTimeOffset? LastCustomerReplyAt { get; init; }
    public DateTimeOffset? LastAgentReplyAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>KPI tile counts for the ticket list, scoped exactly like the list itself.</summary>
public record TicketStatisticsDto
{
    public int OpenCount { get; init; }
    public int UnassignedCount { get; init; }
    public int DueTodayCount { get; init; }
    public int BreachedCount { get; init; }
}

/// <summary>Summary of the owning customer, shown in the ticket detail's left panel.</summary>
public record TicketCustomerSummaryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? Tier { get; init; }
    public string? PrimaryEmail { get; init; }
    public string? PrimaryPhone { get; init; }
    public bool IsBlocked { get; init; }
    public int OpenTicketCount { get; init; }
}

/// <summary>Full ticket, its customer summary and property lookups, for the detail screen.</summary>
public record TicketDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Language { get; init; } = "ar";

    public TicketCustomerSummaryDto Customer { get; init; } = new();

    public Guid CategoryId { get; init; }
    public string CategoryNameEn { get; init; } = string.Empty;
    public string CategoryNameAr { get; init; } = string.Empty;

    public Guid PriorityId { get; init; }
    public string PriorityNameEn { get; init; } = string.Empty;
    public string PriorityNameAr { get; init; } = string.Empty;
    public string PriorityColorHex { get; init; } = string.Empty;

    public Guid StatusId { get; init; }
    public string StatusNameEn { get; init; } = string.Empty;
    public string StatusNameAr { get; init; } = string.Empty;
    public string StatusColorHex { get; init; } = string.Empty;
    public TicketStatusKind StatusKind { get; init; }
    public bool IsTerminal { get; init; }

    public ChannelKey Channel { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }
    public Guid? AssignedAgentId { get; init; }
    public string? AssignedAgentNameEn { get; init; }
    public string? AssignedAgentNameAr { get; init; }

    public DateTimeOffset? FirstResponseDueAt { get; init; }
    public DateTimeOffset? ResolutionDueAt { get; init; }
    public bool IsFirstResponseBreached { get; init; }
    public bool IsResolutionBreached { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public int ReopenCount { get; init; }
    public int CustomerReplyCount { get; init; }
    public int EscalationLevel { get; init; }
    public DateTimeOffset? EscalatedAt { get; init; }
    public Guid? MergedIntoTicketId { get; init; }

    public IReadOnlyList<TicketTagDto> Tags { get; init; } = Array.Empty<TicketTagDto>();

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>Whether the current caller may reply, note, merge, etc. — mirrors permission checks so the UI needs no guesswork.</summary>
    public bool CanUpdate { get; init; }
    public bool CanReply { get; init; }
    public bool CanAddInternalNote { get; init; }
    public bool CanMerge { get; init; }
    public bool CanAssign { get; init; }
    public bool CanChangeStatus { get; init; }
    public bool CanEscalate { get; init; }
}

public record TicketTagDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ColorHex { get; init; } = string.Empty;
}

/// <summary>One entry in the conversation thread.</summary>
public record TicketMessageDto
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public ChannelKey Channel { get; init; }
    public MessageDirection Direction { get; init; }
    public MessageAuthorType AuthorType { get; init; }
    public Guid? AuthorId { get; init; }
    public string? AuthorDisplayName { get; init; }
    public string? Subject { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public string? BodyHtml { get; init; }
    public bool IsInternalNote { get; init; }
    public DateTimeOffset SentAt { get; init; }
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = Array.Empty<AttachmentDto>();
}

public record SavedTicketViewDto
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string FiltersJson { get; init; } = "{}";
    public int DisplayOrder { get; init; }
    public bool IsShared { get; init; }
}

/// <summary>Lookup rows for the create form and list filters, fetched once and cached client-side.</summary>
public record TicketLookupsDto
{
    public IReadOnlyList<TicketCategoryLookupDto> Categories { get; init; } = Array.Empty<TicketCategoryLookupDto>();
    public IReadOnlyList<TicketPriorityLookupDto> Priorities { get; init; } = Array.Empty<TicketPriorityLookupDto>();
    public IReadOnlyList<TicketStatusLookupDto> Statuses { get; init; } = Array.Empty<TicketStatusLookupDto>();
    public IReadOnlyList<DepartmentLookupDto> Departments { get; init; } = Array.Empty<DepartmentLookupDto>();
}

public record TicketCategoryLookupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public Guid? ParentId { get; init; }
    public int Depth { get; init; }
    public string Path { get; init; } = "/";
    public Guid? DefaultPriorityId { get; init; }
    public Guid? DefaultDepartmentId { get; init; }
}

public record TicketPriorityLookupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public int Level { get; init; }
    public string ColorHex { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}

public record TicketStatusLookupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public TicketStatusKind Kind { get; init; }
    public string ColorHex { get; init; } = string.Empty;
    public bool IsTerminal { get; init; }
    public bool PausesSla { get; init; }
    public bool IsDefault { get; init; }
}

public record DepartmentLookupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
}
