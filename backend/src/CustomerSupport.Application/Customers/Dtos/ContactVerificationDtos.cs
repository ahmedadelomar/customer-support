namespace CustomerSupport.Application.Customers.Dtos;

/// <summary>Response for sending a verification code.</summary>
public record SendContactVerificationResultDto
{
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Set only while <c>IContactVerificationSender.IsPlaceholder</c> is true (before CS-301/CS-304
    /// land) — lets an agent see the code in the UI instead of it going nowhere.
    /// </summary>
    public string? DevCode { get; init; }
}

/// <summary>
/// Response for confirming a code. A wrong guess is not an error — it is reported here so the
/// dialog can show "N attempts left" — but an expired/missing/exhausted code IS an error, since
/// there is nothing left to retry against.
/// </summary>
public record ConfirmContactVerificationResultDto
{
    public bool Success { get; init; }
    public int RemainingAttempts { get; init; }
}
