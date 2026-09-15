namespace CustomerSupport.Application.Common.Exceptions;

/// <summary>Thrown when a requested entity does not exist or is outside the caller branch scope.</summary>
public class NotFoundException(string name, object key)
    : Exception($"Entity \"{name}\" ({key}) was not found.");

/// <summary>Thrown when a request breaks a business rule. Surfaces as HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>
/// Thrown when a contact value already belongs to a different customer. Unlike
/// <see cref="ConflictException"/>, this is not a hard refusal — households and companies
/// legitimately share a phone number — so the exception carries the other customer's identity and
/// the caller may resubmit with <c>ConfirmDuplicate = true</c> to proceed anyway.
/// </summary>
public class DuplicateContactException(Guid duplicateCustomerId, string duplicateCustomerName, string message)
    : Exception(message)
{
    public Guid DuplicateCustomerId { get; } = duplicateCustomerId;
    public string DuplicateCustomerName { get; } = duplicateCustomerName;
}

/// <summary>Thrown when the caller is authenticated but lacks the required permission. Surfaces as HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Aggregates FluentValidation failures. Surfaces as HTTP 400 with a per-field problem detail.</summary>
public class ValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation failures occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
