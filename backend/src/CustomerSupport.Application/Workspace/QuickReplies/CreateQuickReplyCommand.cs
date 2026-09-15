using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Workspace;
using FluentValidation;
using MediatR;

namespace CustomerSupport.Application.Workspace.QuickReplies;

/// <summary>Creates a quick reply. Global scope additionally requires <c>workspace.quickreplies.manage.global</c>.</summary>
[RequirePermission(Permissions.Workspace.ManageQuickReplies)]
public record CreateQuickReplyCommand : IRequest<Guid>
{
    /// <summary>"Personal", "Team" or "Global".</summary>
    public string Scope { get; init; } = "Personal";
    public Guid? TeamId { get; init; }
    public string? Shortcut { get; init; }
    public string TitleEn { get; init; } = string.Empty;
    public string TitleAr { get; init; } = string.Empty;
    public string BodyEn { get; init; } = string.Empty;
    public string BodyAr { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public Guid? ChannelId { get; init; }
}

public class CreateQuickReplyCommandValidator : AbstractValidator<CreateQuickReplyCommand>
{
    public CreateQuickReplyCommandValidator()
    {
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

public class CreateQuickReplyCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateQuickReplyCommand, Guid>
{
    public async Task<Guid> Handle(CreateQuickReplyCommand request, CancellationToken cancellationToken)
    {
        if (request.Scope == "Global" && !currentUser.HasPermission(Permissions.Workspace.ManageGlobalQuickReplies))
        {
            throw new ForbiddenException("Creating a global quick reply requires the global quick-replies permission.");
        }

        await QuickReplyShortcutValidation.EnsureUnambiguousAsync(db, currentUser, request.Shortcut, null, cancellationToken);

        var quickReply = new QuickReply
        {
            Scope = request.Scope,
            OwnerId = request.Scope == "Personal" ? currentUser.UserId : null,
            TeamId = request.Scope == "Team" ? request.TeamId : null,
            Shortcut = request.Shortcut,
            Title = new LocalizedText(request.TitleEn, request.TitleAr),
            Body = new LocalizedText(request.BodyEn, request.BodyAr),
            CategoryId = request.CategoryId,
            ChannelId = request.ChannelId,
        };

        db.QuickReplies.Add(quickReply);
        await db.SaveChangesAsync(cancellationToken);
        return quickReply.Id;
    }
}
