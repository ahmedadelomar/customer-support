using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.ChannelAccounts;

/// <summary>Every channel account — mailboxes today, WhatsApp/SMS/web-form accounts once CS-302/304/305 land, since they all share this one table.</summary>
[RequirePermission(Permissions.Channels.Manage)]
public record GetChannelAccountsQuery : IRequest<IReadOnlyList<ChannelAccountDto>>;

public class GetChannelAccountsQueryHandler(IAppDbContext db) : IRequestHandler<GetChannelAccountsQuery, IReadOnlyList<ChannelAccountDto>>
{
    public async Task<IReadOnlyList<ChannelAccountDto>> Handle(GetChannelAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await db.ChannelAccounts.AsNoTracking()
            .Include(a => a.Channel)
            .OrderBy(a => a.Channel.DisplayOrder).ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var connectionIds = accounts.Where(a => a.IntegrationConnectionId is not null)
            .Select(a => a.IntegrationConnectionId!.Value).ToList();

        var hasSecret = await db.IntegrationConnections.AsNoTracking()
            .Where(c => connectionIds.Contains(c.Id) && c.CredentialsEncrypted != null)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        var hasSecretSet = hasSecret.ToHashSet();

        return accounts.Select(a => new ChannelAccountDto
        {
            Id = a.Id,
            ChannelId = a.ChannelId,
            ChannelKey = (int)a.Channel.Key,
            ChannelNameEn = a.Channel.Name.En,
            ChannelNameAr = a.Channel.Name.Ar,
            Name = a.Name,
            Identifier = a.Identifier,
            DefaultDepartmentId = a.DefaultDepartmentId,
            DefaultCategoryId = a.DefaultCategoryId,
            DefaultPriorityId = a.DefaultPriorityId,
            SignatureEn = a.Signature.En,
            SignatureAr = a.Signature.Ar,
            AutoReplyBodyEn = a.AutoReplyBody.En,
            AutoReplyBodyAr = a.AutoReplyBody.Ar,
            SendAutoReply = a.SendAutoReply,
            SettingsJson = a.SettingsJson,
            IsActive = a.IsActive,
            LastPolledAt = a.LastPolledAt,
            LastPollError = a.LastPollError,
            HasWebhookSecret = a.IntegrationConnectionId is { } id && hasSecretSet.Contains(id),
        }).ToList();
    }
}
