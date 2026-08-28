namespace CustomerSupport.Domain.Common;

/// <summary>
/// Owned value object for the bilingual (Arabic / English) strings required by the Platform section.
/// Persisted as two columns, e.g. <c>NameEn</c> / <c>NameAr</c>, via <c>OwnsOne</c>.
/// </summary>
public class LocalizedText
{
    public string En { get; set; } = string.Empty;
    public string Ar { get; set; } = string.Empty;

    public LocalizedText() { }

    public LocalizedText(string en, string ar)
    {
        En = en;
        Ar = ar;
    }

    /// <summary>Returns the value for <paramref name="culture"/> ("ar"/"en"), falling back to the other language.</summary>
    public string For(string culture) =>
        culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? (string.IsNullOrWhiteSpace(Ar) ? En : Ar)
            : (string.IsNullOrWhiteSpace(En) ? Ar : En);
}
