using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Integrations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>See <see cref="IChannelWebhookSecrets"/>. Protected the same way <c>SettingsProvider</c> protects secret system settings: at rest, decrypted only here.</summary>
public class ChannelWebhookSecrets(IAppDbContext db, IDataProtectionProvider dataProtection) : IChannelWebhookSecrets
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("CustomerSupport.ChannelWebhookSecrets");

    public async Task<string?> GetSecretAsync(Guid channelAccountId, CancellationToken ct = default)
    {
        var account = await db.ChannelAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == channelAccountId, ct);

        if (account?.IntegrationConnectionId is not { } connectionId)
        {
            return null;
        }

        var encrypted = await db.IntegrationConnections.AsNoTracking()
            .Where(c => c.Id == connectionId)
            .Select(c => c.CredentialsEncrypted)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrEmpty(encrypted) ? null : _protector.Unprotect(encrypted);
    }

    public async Task SetSecretAsync(Guid channelAccountId, string? secret, CancellationToken ct = default)
    {
        var account = await db.ChannelAccounts.FirstOrDefaultAsync(a => a.Id == channelAccountId, ct)
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ChannelAccount), channelAccountId);

        var isBlank = string.IsNullOrWhiteSpace(secret);

        IntegrationConnection? connection = account.IntegrationConnectionId is { } id
            ? await db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == id, ct)
            : null;

        if (isBlank)
        {
            if (connection is not null)
            {
                connection.CredentialsEncrypted = null;
                connection.Status = IntegrationStatus.NotConfigured;
            }
            return;
        }

        connection ??= new IntegrationConnection
        {
            Type = IntegrationType.EmailProvider,
            Name = $"{account.Name} webhook secret",
            Provider = "webhook-hmac",
        };

        connection.CredentialsEncrypted = _protector.Protect(secret!);
        connection.Status = IntegrationStatus.Connected;

        if (connection.Id == default)
        {
            db.IntegrationConnections.Add(connection);
        }

        account.IntegrationConnectionId = connection.Id;
    }
}
