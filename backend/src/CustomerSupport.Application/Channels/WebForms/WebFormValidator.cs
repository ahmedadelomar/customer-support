using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CustomerSupport.Application.Common.Localization;

namespace CustomerSupport.Application.Channels.WebForms;

/// <summary>
/// Validates a public submission against a form's own field schema — the public endpoint's last
/// line of defence, since the schema-generated client form can be bypassed entirely by calling the
/// API directly.
/// </summary>
public interface IWebFormValidator
{
    /// <summary>Errors keyed by field key; empty when the payload is valid.</summary>
    IDictionary<string, string[]> Validate(IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> payload);
}

public class WebFormValidator(IMessageLocalizer localizer) : IWebFormValidator
{
    /// <summary>
    /// An administrator-authored pattern is untrusted input to the regex engine itself — a match
    /// timeout is what actually stops a catastrophic-backtracking pattern from hanging a request,
    /// not just "being careful" with the pattern's shape.
    /// </summary>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly ConcurrentDictionary<string, Regex> CompiledPatterns = new();

    public IDictionary<string, string[]> Validate(
        IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> payload)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var field in fields)
        {
            payload.TryGetValue(field.Key, out var raw);
            var value = raw?.Trim() ?? string.Empty;

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                errors[field.Key] = [localizer[MessageKeys.FieldRequired]];
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue; // Optional and blank — nothing further to check.
            }

            var fieldErrors = new List<string>();

            if (field.MaxLength is { } maxLength && value.Length > maxLength)
            {
                fieldErrors.Add(localizer[MessageKeys.FieldTooLong]);
            }

            if (!string.IsNullOrWhiteSpace(field.Pattern) && !SafeIsMatch(field.Pattern, value))
            {
                fieldErrors.Add(localizer[MessageKeys.FieldInvalidFormat]);
            }

            if (field.Type is "select" or "multiselect" && field.Options is { Count: > 0 } options)
            {
                var allowed = options.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
                var selected = field.Type == "multiselect"
                    ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : [value];

                if (selected.Any(v => !allowed.Contains(v)))
                {
                    fieldErrors.Add(localizer[MessageKeys.FieldInvalidOption]);
                }
            }

            if (field.Type == "email" && !SafeIsMatch(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", value))
            {
                fieldErrors.Add(localizer[MessageKeys.FieldInvalidFormat]);
            }

            if (fieldErrors.Count > 0)
            {
                errors[field.Key] = fieldErrors.ToArray();
            }
        }

        return errors;
    }

    private static bool SafeIsMatch(string pattern, string value)
    {
        var regex = CompiledPatterns.GetOrAdd(pattern, p => new Regex(p, RegexOptions.None, MatchTimeout));

        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
