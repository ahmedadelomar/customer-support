using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.ChannelAccounts;

[RequirePermission(Permissions.Channels.Manage)]
public record CreateChannelAccountCommand(ChannelAccountRequest Request) : IRequest<Guid>;

public class CreateChannelAccountCommandValidator : AbstractValidator<CreateChannelAccountCommand>
{
    public CreateChannelAccountCommandValidator()
    {
        RuleFor(x => x.Request.ChannelId).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Identifier).NotEmpty().MaximumLength(256);
    }
}

public class CreateChannelAccountCommandHandler(IAppDbContext db, IChannelWebhookSecrets secrets)
    : IRequestHandler<CreateChannelAccountCommand, Guid>
{
    public async Task<Guid> Handle(CreateChannelAccountCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (!await db.Channels.AnyAsync(c => c.Id == request.ChannelId, ct))
        {
            throw new NotFoundException(nameof(Domain.Channels.Channel), request.ChannelId);
        }

        var account = new ChannelAccount
        {
            ChannelId = request.ChannelId,
            Name = request.Name,
            Identifier = request.Identifier,
            DefaultDepartmentId = request.DefaultDepartmentId,
            DefaultCategoryId = request.DefaultCategoryId,
            DefaultPriorityId = request.DefaultPriorityId,
            Signature = new LocalizedText(request.SignatureEn, request.SignatureAr),
            AutoReplyBody = new LocalizedText(request.AutoReplyBodyEn, request.AutoReplyBodyAr),
            SendAutoReply = request.SendAutoReply,
            SettingsJson = request.SettingsJson,
            IsActive = request.IsActive,
        };

        db.ChannelAccounts.Add(account);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            await secrets.SetSecretAsync(account.Id, request.WebhookSecret, ct);
            await db.SaveChangesAsync(ct);
        }

        return account.Id;
    }
}

[RequirePermission(Permissions.Channels.Manage)]
public record UpdateChannelAccountCommand(Guid Id, ChannelAccountRequest Request) : IRequest;

public class UpdateChannelAccountCommandValidator : AbstractValidator<UpdateChannelAccountCommand>
{
    public UpdateChannelAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Identifier).NotEmpty().MaximumLength(256);
    }
}

public class UpdateChannelAccountCommandHandler(IAppDbContext db, IChannelWebhookSecrets secrets)
    : IRequestHandler<UpdateChannelAccountCommand>
{
    public async Task Handle(UpdateChannelAccountCommand command, CancellationToken ct)
    {
        var account = await db.ChannelAccounts.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new NotFoundException(nameof(ChannelAccount), command.Id);

        var request = command.Request;

        account.Name = request.Name;
        account.Identifier = request.Identifier;
        account.DefaultDepartmentId = request.DefaultDepartmentId;
        account.DefaultCategoryId = request.DefaultCategoryId;
        account.DefaultPriorityId = request.DefaultPriorityId;
        account.Signature = new LocalizedText(request.SignatureEn, request.SignatureAr);
        account.AutoReplyBody = new LocalizedText(request.AutoReplyBodyEn, request.AutoReplyBodyAr);
        account.SendAutoReply = request.SendAutoReply;
        account.SettingsJson = request.SettingsJson;
        account.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        // null = leave unchanged; a non-null value (including "") sets or clears it.
        if (request.WebhookSecret is not null)
        {
            await secrets.SetSecretAsync(account.Id, request.WebhookSecret, ct);
            await db.SaveChangesAsync(ct);
        }
    }
}

[RequirePermission(Permissions.Channels.Manage)]
public record DeleteChannelAccountCommand(Guid Id) : IRequest;

public class DeleteChannelAccountCommandHandler(IAppDbContext db) : IRequestHandler<DeleteChannelAccountCommand>
{
    public async Task Handle(DeleteChannelAccountCommand command, CancellationToken ct)
    {
        var account = await db.ChannelAccounts.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new NotFoundException(nameof(ChannelAccount), command.Id);

        var inUse = await db.Tickets.AnyAsync(t => t.ChannelAccountId == command.Id, ct);
        if (inUse)
        {
            throw new ConflictException("This account has tickets linked to it and cannot be deleted — deactivate it instead.");
        }

        db.ChannelAccounts.Remove(account);
        await db.SaveChangesAsync(ct);
    }
}
