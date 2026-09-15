using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Domain.Tickets;

namespace CustomerSupport.Application.Tickets;

/// <summary>
/// The one place "is this ticket read-only?" is decided — reused by every mutation except
/// <c>ChangeTicketStatusCommand</c>, which is deliberately the escape valve that lets an agent
/// manually reopen a terminal ticket.
/// </summary>
public static class TicketReadOnlyGuard
{
    /// <param name="ticket">The ticket being mutated.</param>
    /// <param name="isTerminal">The current status's <c>IsTerminal</c> flag — passed explicitly rather
    /// than read off <see cref="Ticket.Status"/> so callers that already loaded the status separately
    /// don't need an extra <c>Include</c> just for this check.</param>
    public static void EnsureEditable(Ticket ticket, bool isTerminal)
    {
        if (ticket.MergedIntoTicketId is not null)
        {
            throw new ConflictException($"Ticket {ticket.Number} was merged into another ticket and is read-only.");
        }

        if (isTerminal)
        {
            throw new ConflictException(
                $"Ticket {ticket.Number} is closed and read-only. Reopen it via its status to make further changes.");
        }
    }
}
