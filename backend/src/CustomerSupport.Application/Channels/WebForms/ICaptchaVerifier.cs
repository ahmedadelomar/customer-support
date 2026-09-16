namespace CustomerSupport.Application.Channels.WebForms;

/// <summary>
/// Verifies a captcha token with whichever provider is configured. Implemented in Infrastructure —
/// same "the real provider is somebody else's story" shape as <c>IEmailChannelSender</c> and
/// <c>IContactVerificationSender</c>: this codebase has no live captcha provider account, so the
/// registered implementation only checks that a token was actually supplied.
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, CancellationToken ct = default);
}
