using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>
/// Renders a quick reply's draft body against a sample ticket before it has even been saved — the
/// management screen's live preview. Not in the story's own API contract table (which only lists
/// render-by-id for an already-saved reply), but there is no other way to preview an unsaved
/// draft against the real, server-side resolver, which is exactly what the story's "resolution is
/// server-side" rule calls for — a client-side re-implementation would create the second place the
/// token vocabulary lives that rule exists to prevent.
/// </summary>
[RequirePermission(Permissions.Workspace.ManageQuickReplies)]
public record PreviewQuickReplyCommand : IRequest<RenderResult>
{
    public Guid TicketId { get; init; }
    public string BodyEn { get; init; } = string.Empty;
    public string BodyAr { get; init; } = string.Empty;
}

public class PreviewQuickReplyCommandValidator : AbstractValidator<PreviewQuickReplyCommand>
{
    public PreviewQuickReplyCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
    }
}

public class PreviewQuickReplyCommandHandler(IAppDbContext db, ICurrentUser currentUser, IPlaceholderResolver resolver)
    : IRequestHandler<PreviewQuickReplyCommand, RenderResult>
{
    public async Task<RenderResult> Handle(PreviewQuickReplyCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.TicketId);

        var template = ticket.Language == "ar" ? request.BodyAr : request.BodyEn;

        return await resolver.RenderAsync(template, request.TicketId, cancellationToken);
    }
}
