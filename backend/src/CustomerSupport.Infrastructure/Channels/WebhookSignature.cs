using System.Security.Cryptography;
using System.Text;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// HMAC-SHA256 verification for inbound provider webhooks, shared by email (CS-301) today and
/// WhatsApp/SMS (CS-302/304) next — every provider's "prove this call is really from you" scheme
/// reduces to the same check once the raw body and a shared secret are in hand.
/// </summary>
public static class WebhookSignature
{
    /// <summary>Header carrying the signature: <c>sha256=&lt;lowercase hex HMAC-SHA256 of the raw body&gt;</c>.</summary>
    public const string HeaderName = "X-Webhook-Signature";

    public static string Compute(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }

    /// <summary>Constant-time comparison — a timing side-channel on this check would leak the secret one byte at a time.</summary>
    public static bool Verify(string rawBody, string? providedSignature, string secret)
    {
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            return false;
        }

        var expected = Compute(rawBody, secret);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature.Trim());

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
