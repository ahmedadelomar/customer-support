using CustomerSupport.Application.Common.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Infrastructure;

/// <summary>
/// Blocks every API call except signing out and changing the password while the caller still carries
/// the <c>must_change_password</c> claim (Security &amp; Administration / Users and roles).
/// </summary>
/// <remarks>
/// Enforced here rather than per-handler so a future endpoint cannot forget it. The response carries
/// <c>"code": "must_change_password"</c> so the Angular error interceptor can route to the
/// change-password screen instead of showing a toast the user cannot act on.
/// </remarks>
public class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPaths =
    [
        "/api/auth/change-password",
        "/api/auth/logout",
        "/api/auth/refresh",
        "/api/auth/login",
    ];

    public async Task InvokeAsync(HttpContext context, IMessageLocalizer localizer)
    {
        var mustChange = context.User.Identity?.IsAuthenticated == true &&
                         context.User.HasClaim("must_change_password", "true");

        if (!mustChange || IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = localizer[MessageKeys.PasswordChangeRequired],
            Detail = localizer[MessageKeys.PasswordChangeRequiredDetail],
            Instance = $"{context.Request.Method} {context.Request.Path}",
        };

        problem.Extensions["code"] = "must_change_password";

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }

    private static bool IsAllowed(PathString path) =>
        !path.StartsWithSegments("/api") ||
        AllowedPaths.Any(allowed => path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase));
}
