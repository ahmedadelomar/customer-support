namespace CustomerSupport.Application.Channels;

/// <summary>
/// The webhook-verification secret behind one <c>ChannelAccount</c>. A secret is itself a
/// credential — leaking it lets an attacker forge valid inbound webhook calls — so it lives in
/// <c>IntegrationConnection.CredentialsEncrypted</c> exactly like every other provider credential in
/// this product, never inline on the channel account. Implemented in Infrastructure, where
/// DataProtection lives.
/// </summary>
public interface IChannelWebhookSecrets
{
    /// <summary>Null when the account has no secret configured yet — the webhook endpoint refuses calls in that state rather than skipping verification.</summary>
    Task<string?> GetSecretAsync(Guid channelAccountId, CancellationToken ct = default);

    /// <summary>Creates or updates the backing <c>IntegrationConnection</c> and links it to the account. A null or empty secret clears it.</summary>
    Task SetSecretAsync(Guid channelAccountId, string? secret, CancellationToken ct = default);
}
