namespace CustomerSupport.Application.Common.Security;

/// <summary>
/// Declares the permission a request requires. Enforced by <c>AuthorizationBehaviour</c> in the
/// MediatR pipeline, so authorisation holds no matter which transport invoked the handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
