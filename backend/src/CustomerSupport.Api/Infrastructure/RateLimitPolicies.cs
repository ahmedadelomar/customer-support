namespace CustomerSupport.Api.Infrastructure;

/// <summary>Named rate-limiting policies, so the controller attribute and the registration cannot drift.</summary>
public static class RateLimitPolicies
{
    /// <summary>Sign-in attempts, partitioned per client IP. Slows credential stuffing without locking real users out.</summary>
    public const string Login = "login";

    /// <summary>
    /// Public web form submissions, partitioned per client IP. A coarse second layer alongside the
    /// precise per-form-per-IP check in <c>SubmitWebFormCommand</c>, since that check costs a query.
    /// </summary>
    public const string WebFormSubmit = "webform-submit";
}
