using System.Text.RegularExpressions;

namespace CustomerSupport.Application.Workspace.Collaboration;

/// <summary>One structured mention found inside a note body.</summary>
public record ParsedMention(Guid UserId, string DisplayName);

/// <summary>
/// Parses the structured mention marker the editor produces — <c>@[Display Name](userId)</c> — so
/// mentions are deterministic rather than guessed at by re-scanning free text for names at render
/// time (per the story's own rule). A malformed or absent marker simply yields no mentions; nothing
/// here ever fails the note itself.
/// </summary>
public static class MentionParser
{
    private static readonly Regex Pattern =
        new(@"@\[([^\]]+)\]\(([0-9a-fA-F-]{36})\)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    /// <summary>Distinct mentions in source order, deduplicated by user id.</summary>
    public static IReadOnlyList<ParsedMention> Parse(string bodyText)
    {
        if (string.IsNullOrEmpty(bodyText))
        {
            return [];
        }

        var seen = new HashSet<Guid>();
        var results = new List<ParsedMention>();

        foreach (Match match in Pattern.Matches(bodyText))
        {
            if (!Guid.TryParse(match.Groups[2].Value, out var userId) || !seen.Add(userId))
            {
                continue;
            }

            results.Add(new ParsedMention(userId, match.Groups[1].Value));
        }

        return results;
    }

    /// <summary>Renders <c>@[Display Name](userId)</c> markers as plain <c>@Display Name</c> text, for excerpts and previews.</summary>
    public static string ToPlainText(string bodyText) =>
        string.IsNullOrEmpty(bodyText) ? bodyText : Pattern.Replace(bodyText, "@$1");
}
