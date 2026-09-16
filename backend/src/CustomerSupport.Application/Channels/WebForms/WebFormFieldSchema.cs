using System.Text.Json;

namespace CustomerSupport.Application.Channels.WebForms;

/// <summary>One selectable option on a `select`/`multiselect` field.</summary>
public record WebFormFieldOption(string Value, string LabelEn, string LabelAr);

/// <summary>
/// One field descriptor from a <c>WebFormDefinition.FieldsJson</c> array (Communication Channels /
/// Web forms). `MapTo` — one of <c>customerEmail</c>, <c>customerPhone</c>, <c>customerName</c>,
/// <c>subject</c>, <c>description</c>, <c>categoryCode</c>, <c>priorityCode</c> — routes an answer
/// onto the created ticket; an unmapped field is appended to the description as a labelled line.
/// </summary>
public record WebFormField(
    string Key,
    string Type,
    string LabelEn,
    string LabelAr,
    string? PlaceholderEn,
    string? PlaceholderAr,
    bool Required,
    int? MaxLength,
    string? Pattern,
    IReadOnlyList<WebFormFieldOption>? Options,
    string? MapTo);

/// <summary>(De)serialises the field list stored in `FieldsJson`, so every reader/writer agrees on the shape.</summary>
public static class WebFormFieldSchemaSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<WebFormField> fields) => JsonSerializer.Serialize(fields, Options);

    public static IReadOnlyList<WebFormField> Deserialize(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyList<WebFormField>>(json, Options) ?? [];
}
