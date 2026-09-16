using System.Text;

namespace CustomerSupport.Application.Common.Localization;

/// <summary>
/// Folds the Arabic spelling variants people type interchangeably, so search matches what they meant
/// (Platform / Bilingual UI and data).
/// </summary>
/// <remarks>
/// Without this, searching "أحمد" misses a customer stored as "احمد" — the same name, differing only
/// by a hamza the typist may or may not have entered. Apply it to BOTH the stored value and the query
/// term; normalising only one side is worse than not normalising at all, because it fails asymmetrically.
/// </remarks>
public static class ArabicText
{
    /// <summary>
    /// Folds alef forms (أ إ آ ٱ → ا), alef maqsura (ى → ي), taa marbuta (ة → ه), and the hamza
    /// carriers (ؤ → و, ئ → ي); strips tatweel and the combining diacritics.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            switch (ch)
            {
                case 'أ': // أ
                case 'إ': // إ
                case 'آ': // آ
                case 'ٱ': // ٱ
                    builder.Append('ا'); // ا
                    break;

                case 'ى': // ى
                    builder.Append('ي'); // ي
                    break;

                case 'ة': // ة
                    builder.Append('ه'); // ه
                    break;

                case 'ؤ': // ؤ
                    builder.Append('و'); // و
                    break;

                case 'ئ': // ئ
                    builder.Append('ي'); // ي
                    break;

                case 'ـ': // tatweel, a purely decorative elongation
                    break;

                default:
                    // Combining marks: fatha through sukun, plus the superscript alef.
                    if (ch is >= 'ً' and <= 'ْ' or 'ٰ')
                    {
                        break;
                    }

                    builder.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>True when <paramref name="haystack"/> contains <paramref name="needle"/> once both are folded.</summary>
    public static bool ContainsNormalized(string? haystack, string? needle) =>
        Normalize(haystack).Contains(Normalize(needle), StringComparison.Ordinal);
}
