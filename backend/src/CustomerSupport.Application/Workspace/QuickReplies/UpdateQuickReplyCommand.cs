using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>
/// Edits a quick reply. Only the owner (personal), a manager (any scope), or a manager holding the
/// global permission (global scope) may edit — checked here rather than via a single permission
/// attribute, since the rule depends on the row's own scope and, for personal replies, its owner.
/// </summary>
[RequirePermission(Permissions.Workspace.ManageQuickReplies)]
public record UpdateQuickReplyCommand : IRequest
{
    public Guid Id { get; init; }
    public string Scope { get; init; } = "Personal";
    public Guid? TeamId { get; init; }
    public string? Shortcut { get; init; }
    public string TitleEn { get; init; } = string.Empty;
    public string TitleAr { get; init; } = string.Empty;
    public string BodyEn { get; init; } = string.Empty;
    public string BodyAr { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public Guid? ChannelId { get; init; }
    public bool IsActive { get; init; } = true;
}

public class UpdateQuickReplyCommandValidator : AbstractValidator<UpdateQuickReplyCommand>
{
    public UpdateQuickReplyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Scope).Must(s => s is "Personal" or "Team" or "Global");
        RuleFor(x => x.TeamId).NotEmpty().When(x => x.Scope == "Team");
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BodyEn).NotEmpty();
        RuleFor(x => x.BodyAr).NotEmpty();
        RuleFor(x => x.Shortcut).MaximumLength(50).Matches("^/[a-z0-9_-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.Shortcut))
            .WithMessage("The shortcut must look like /word — lowercase letters, digits, hyphens and underscores only.");
    }
}

public class UpdateQuickReplyCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateQuickReplyCommand>
{
    public async Task Handle(UpdateQuickReplyCommand request, CancellationToken cancellationToken)
    {
        var quickReply = await db.QuickReplies.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.QuickReply), request.Id);

        var canManageGlobal = currentUser.HasPermission(Permissions.Workspace.ManageGlobalQuickReplies);

        if ((quickReply.Scope == "Global" || request.Scope == "Global") && !canManageGlobal)
        {
            throw new ForbiddenException("Editing a global quick reply requires the global quick-replies permission.");
        }

        // `workspace.quickreplies.manage` (already required to reach this handler) is the broad
        // "may use the quick-reply endpoints" grant every agent typically holds — it is not itself
        // license to edit a colleague's personal reply, so ownership is still checked independently.
        if (quickReply.Scope == "Personal" && quickReply.OwnerId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the owner may edit a personal quick reply.");
        }

        await QuickReplyShortcutValidation.EnsureUnambiguousAsync(db, currentUser, request.Shortcut, request.Id, cancellationToken);

        quickReply.Scope = request.Scope;
        quickReply.OwnerId = request.Scope == "Personal" ? (quickReply.OwnerId ?? currentUser.UserId) : null;
        quickReply.TeamId = request.Scope == "Team" ? request.TeamId : null;
        quickReply.Shortcut = request.Shortcut;
        quickReply.Title = new LocalizedText(request.TitleEn, request.TitleAr);
        quickReply.Body = new LocalizedText(request.BodyEn, request.BodyAr);
        quickReply.CategoryId = request.CategoryId;
        quickReply.ChannelId = request.ChannelId;
        quickReply.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
