using CustomerSupport.Application.Channels.WebForms;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Channels.WebForms;

/// <summary>
/// Stands in for a real captcha provider (hCaptcha, reCAPTCHA, ...) — this codebase has no live
/// provider account. Only checks that a token was actually supplied, which is enough to exercise
/// the "captcha required and missing" rejection path honestly without pretending to verify anything.
/// Replace the DI registration once a provider is configured.
/// </summary>
public class LoggingCaptchaVerifier(ILogger<LoggingCaptchaVerifier> logger) : ICaptchaVerifier
{
    public Task<bool> VerifyAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(false);
        }

        logger.LogInformation("Captcha token accepted without provider verification — no captcha provider is wired up yet.");
        return Task.FromResult(true);
    }
}
