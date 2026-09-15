using System.Text.RegularExpressions;
using CustomerSupport.Domain.Enums;

namespace CustomerSupport.Application.Common;

/// <summary>
/// Format checks that must run against the <see cref="ContactNormalizer"/>-normalised value, not
/// the raw input — a phone typed as <c>+966 50 123 4567</c> is exactly the format the product is
/// meant to accept, and it only matches a digits-only pattern after normalisation strips the
/// spaces. Validating the raw string against that pattern (as this codebase did before) rejects
/// the most natural way anyone types a phone number.
/// </summary>
public static partial class ContactValueValidator
{
    [GeneratedRegex(@"^\+?[0-9]{7,15}$")]
    private static partial Regex PhonePattern();

    /// <summary>True when the normalised form of <paramref name="rawValue"/> is 7–15 digits with an optional leading plus.</summary>
    public static bool IsValidPhone(string? rawValue) =>
        ContactNormalizer.Normalize(rawValue) is { Length: > 0 } normalized && PhonePattern().IsMatch(normalized);

    /// <summary>Dispatches to the right check for the contact type, or true for types with no fixed format (address, label, etc.).</summary>
    public static bool IsValid(ContactType type, string? rawValue) => type switch
    {
        ContactType.Mobile or ContactType.Phone or ContactType.WhatsApp => IsValidPhone(rawValue),
        _ => true,
    };
}
