using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>
/// Resolves a quick reply's body against a ticket and increments its usage count — called the
/// moment the agent inserts the reply (picker selection or shortcut expansion), not when the message
/// is actually sent, so "most used" reflects real usage even if the agent edits the draft afterward.
/// Message attribution (<c>TicketMessage.QuickReplyId</c>) happens separately, in
/// <c>ReplyToTicketCommand</c>, only if the message is actually sent.
/// </summary>
[RequirePermission(Permissions.Tickets.Reply)]
public record RenderQuickReplyCommand : IRequest<RenderResult>
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
}

public class RenderQuickReplyCommandValidator : AbstractValidator<RenderQuickReplyCommand>
{
    public RenderQuickReplyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TicketId).NotEmpty();
    }
}

public class RenderQuickReplyCommandHandler(IAppDbContext db, ICurrentUser currentUser, IPlaceholderResolver resolver)
    : IRequestHandler<RenderQuickReplyCommand, RenderResult>
{
    public async Task<RenderResult> Handle(RenderQuickReplyCommand request, CancellationToken cancellationToken)
    {
        var visible = await QuickReplyVisibility.ApplyAsync(db.QuickReplies, db, currentUser, cancellationToken);

        var quickReply = await visible.FirstOrDefaultAsync(q => q.Id == request.Id && q.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.QuickReply), request.Id);

        var ticketVisible = await db.Tickets.WhereBranchAccessible(currentUser).WhereTicketVisible(currentUser)
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!ticketVisible)
        {
            throw new NotFoundException(nameof(Domain.Tickets.Ticket), request.TicketId);
        }

        var ticket = await db.Tickets.AsNoTracking().FirstAsync(t => t.Id == request.TicketId, cancellationToken);
        var template = quickReply.Body.For(ticket.Language);

        var result = await resolver.RenderAsync(template, request.TicketId, cancellationToken);

        quickReply.UsageCount += 1;
        await db.SaveChangesAsync(cancellationToken);

        return result;
    }
}
