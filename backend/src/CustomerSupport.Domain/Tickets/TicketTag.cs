using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Tickets;

/// <summary>Join row between <see cref="Ticket"/> and <see cref="Tag"/>.</summary>
public class TicketTag : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public DateTimeOffset TaggedAt { get; set; }
    public Guid? TaggedById { get; set; }
}
