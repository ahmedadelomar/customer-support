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

/// <summary>
/// Thrown when assigning a ticket to an agent who is unavailable or at capacity. Unlike
/// <see cref="ConflictException"/>, this is sometimes meant to be overridden — the caller may resubmit
/// with <c>Force = true</c> — except when <see cref="IsHardBlock"/> is set, for a deactivated agent,
/// which no amount of forcing may bypass.
/// </summary>
public class AssignmentWarningException(string message, bool isHardBlock, int openTickets, int? cap)
    : Exception(message)
{
    public bool IsHardBlock { get; } = isHardBlock;
    public int OpenTickets { get; } = openTickets;
    public int? Cap { get; } = cap;
}

/// <summary>Aggregates FluentValidation failures. Surfaces as HTTP 400 with a per-field problem detail.</summary>
public class ValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation failures occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
