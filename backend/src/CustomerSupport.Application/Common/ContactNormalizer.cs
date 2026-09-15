namespace CustomerSupport.Application.Common;

/// <summary>
/// The one and only normalisation rule for contact values. Inbound message routing (Section 3)
/// matches on the normalised value, so every path that writes a <c>CustomerContact</c> — customer
/// creation, adding a contact, editing one — must call this and nothing else. A second
/// implementation, even a slightly different one, silently misroutes messages or creates duplicates.
/// </summary>
public static class ContactNormalizer
{
    /// <summary>Lower-cases emails and reduces phone numbers to digits with an optional leading plus.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Contains('@')
            ? trimmed.ToLowerInvariant()
            : new string(trimmed.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
    }
}
