using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Files;

/// <summary>
/// Answers "may the current caller touch this owning record?" for a polymorphic
/// <see cref="Domain.Files.Attachment"/>. This is the one gate every attachment upload and download
/// passes through — an unguessable id is not authorisation, so both actions call this rather than
/// trusting the id alone.
/// </summary>
public interface IAttachmentOwnerAuthorizer
{
    /// <summary>Returns false when the owner does not exist, is out of branch scope, or the caller lacks the permission.</summary>
    Task<bool> CanAccessAsync(string ownerType, Guid ownerId, CancellationToken ct = default);
}

/// <summary>
/// One branch per attachable aggregate. Adding a new one is deliberate: an <c>ownerType</c>
/// not listed here is refused rather than silently allowed, so a typo or a forgotten case can never
/// turn into an access-control hole.
/// </summary>
public class AttachmentOwnerAuthorizer(IAppDbContext db, ICurrentUser currentUser) : IAttachmentOwnerAuthorizer
{
    public Task<bool> CanAccessAsync(string ownerType, Guid ownerId, CancellationToken ct = default) => ownerType switch
    {
        "Customer" => CanAccessCustomerAsync(ownerId, ct),
        "CustomerNote" => CanAccessCustomerNoteAsync(ownerId, ct),
        "Ticket" => CanAccessTicketAsync(ownerId, ct),
        "TicketMessage" => CanAccessTicketMessageAsync(ownerId, ct),
        "KbArticle" => CanAccessKbArticleAsync(ownerId, ct),
        _ => Task.FromResult(false),
    };

    private async Task<bool> CanAccessCustomerAsync(Guid customerId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.Customers.View))
        {
            return false;
        }

        return await db.Customers.WhereBranchAccessible(currentUser).AnyAsync(c => c.Id == customerId, ct);
    }

    private async Task<bool> CanAccessCustomerNoteAsync(Guid noteId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.Customers.ViewNotes))
        {
            return false;
        }

        // A note carries no BranchId of its own — scope through the customer it belongs to.
        var customerId = await db.CustomerNotes
            .Where(n => n.Id == noteId && !n.IsDeleted)
            .Select(n => (Guid?)n.CustomerId)
            .FirstOrDefaultAsync(ct);

        return customerId is { } id &&
               await db.Customers.WhereBranchAccessible(currentUser).AnyAsync(c => c.Id == id, ct);
    }

    private async Task<bool> CanAccessTicketAsync(Guid ticketId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.Tickets.View))
        {
            return false;
        }

        return await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == ticketId, ct);
    }

    private async Task<bool> CanAccessTicketMessageAsync(Guid messageId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.Tickets.View))
        {
            return false;
        }

        var ticketId = await db.TicketMessages
            .Where(m => m.Id == messageId && !m.IsDeleted)
            .Select(m => (Guid?)m.TicketId)
            .FirstOrDefaultAsync(ct);

        return ticketId is { } id &&
               await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
                   .AnyAsync(t => t.Id == id, ct);
    }

    private async Task<bool> CanAccessKbArticleAsync(Guid articleId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.KnowledgeBase.View))
        {
            return false;
        }

        return await db.KbArticles.WhereBranchAccessible(currentUser).AnyAsync(a => a.Id == articleId, ct);
    }
}
