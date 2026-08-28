using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;

namespace CustomerSupport.Application.Common.Behaviours;

/// <summary>
/// Enforces <see cref="RequirePermissionAttribute"/> declarations. All declared permissions must be
/// held, which keeps the check explicit and auditable per request type.
/// </summary>
public class AuthorizationBehaviour<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var required = (RequirePermissionAttribute[])Attribute.GetCustomAttributes(
            typeof(TRequest), typeof(RequirePermissionAttribute));

        if (required.Length != 0)
        {
            if (!currentUser.IsAuthenticated)
            {
                throw new ForbiddenException("Authentication is required for this operation.");
            }

            var missing = required
                .Select(a => a.Permission)
                .Where(p => !currentUser.HasPermission(p))
                .ToList();

            if (missing.Count != 0)
            {
                throw new ForbiddenException($"Missing permission(s): {string.Join(", ", missing)}.");
            }
        }

        return await next();
    }
}
