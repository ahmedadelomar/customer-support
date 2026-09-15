using System.Net;
using System.Text.RegularExpressions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Infrastructure.Persistence;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Adds timeline rows to the current unit of work. Nothing is saved here: the caller saves, so the
/// interaction and the state change it describes commit together or not at all — mirrors
/// <see cref="TicketEventRecorder"/>.
/// </summary>
public partial class InteractionRecorder(AppDbContext db, IDateTimeProvider clock) : IInteractionRecorder
{
    private const int MaxPreviewLength = 1000;

    public void Record(
        Guid customerId,
        ChannelKey channel,
        MessageDirection direction,
        string? subject,
        string? preview,
        Guid? ticketId = null,
        string? sourceType = null,
        Guid? sourceId = null,
        Guid? agentId = null)
    {
        db.Interactions.Add(new Interaction
        {
            CustomerId = customerId,
            TicketId = ticketId,
            SourceId = sourceId,
            SourceType = sourceType,
            Channel = channel,
            Direction = direction,
            Subject = subject,
            Preview = PreparePreview(preview),
            AgentId = agentId,
            OccurredAt = clock.UtcNow,
        });

        UpdateLastInteractionAt(customerId);
    }

    /// <summary>
    /// Bumps <c>Customer.LastInteractionAt</c>, the column the customer list sorts on. Uses whatever
    /// tracked instance already exists in this unit of work when there is one — updating a second,
    /// freshly-attached instance with the same key would throw ("another instance with the same key
    /// value is already being tracked") — and otherwise attaches a stub and marks only this one
    /// property modified, so no other column on the row is touched or overwritten.
    /// </summary>
    private void UpdateLastInteractionAt(Guid customerId)
    {
        var tracked = db.ChangeTracker.Entries<Customer>()
            .Select(e => e.Entity)
            .FirstOrDefault(c => c.Id == customerId);

        if (tracked is not null)
        {
            tracked.LastInteractionAt = clock.UtcNow;
            return;
        }

        var stub = new Customer { Id = customerId };
        db.Customers.Attach(stub);
        stub.LastInteractionAt = clock.UtcNow;
        db.Entry(stub).Property(x => x.LastInteractionAt).IsModified = true;
    }

    /// <summary>
    /// Strips markup and collapses whitespace so the timeline never has to parse HTML or load a
    /// full message body just to render a preview line, then truncates to a fixed length.
    /// </summary>
    private static string? PreparePreview(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var withoutTags = HtmlTagPattern().Replace(raw, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var collapsed = WhitespacePattern().Replace(decoded, " ").Trim();

        return collapsed.Length > MaxPreviewLength ? collapsed[..MaxPreviewLength] : collapsed;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
