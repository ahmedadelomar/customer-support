namespace CustomerSupport.Application.Common.Exceptions;

/// <summary>Thrown when a requested entity does not exist or is outside the caller branch scope.</summary>
public class NotFoundException(string name, object key)
    : Exception($"Entity \"{name}\" ({key}) was not found.");

/// <summary>Thrown when a request breaks a business rule. Surfaces as HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Thrown when the caller is authenticated but lacks the required permission. Surfaces as HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Aggregates FluentValidation failures. Surfaces as HTTP 400 with a per-field problem detail.</summary>
public class ValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation failures occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
