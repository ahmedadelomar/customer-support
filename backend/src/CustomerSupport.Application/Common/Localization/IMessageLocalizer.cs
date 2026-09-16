namespace CustomerSupport.Application.Common.Localization;

/// <summary>
/// Translates server-produced, user-visible text — problem-details titles, validation messages and
/// notification copy (Platform / Bilingual UI and data).
/// </summary>
/// <remarks>
/// Deliberately not .NET's `IStringLocalizer`: that binds to satellite `.resx` assemblies and a
/// per-request `CurrentUICulture`, whereas this codebase needs to render text in a language chosen
/// per *recipient* — the customer's for outbound messages, the agent's for internal notifications —
/// which is often not the language of whoever triggered the action. Passing the culture explicitly
/// makes that difference impossible to get wrong by accident.
/// </remarks>
public interface IMessageLocalizer
{
    /// <summary>Text for <paramref name="key"/> in the request's culture, falling back to the key itself.</summary>
    string this[string key] { get; }

    /// <summary>Text for <paramref name="key"/> in an explicit culture (<c>ar</c> or <c>en</c>).</summary>
    string For(string key, string culture);

    /// <summary>Formats a message with positional arguments, e.g. <c>"{0} open ticket(s) remain"</c>.</summary>
    string Format(string key, params object[] args);
}

/// <summary>Message keys the server localises. Constants rather than literals so a typo fails the build.</summary>
public static class MessageKeys
{
    public const string ValidationFailed = "error.validationFailed";
    public const string NotFound = "error.notFound";
    public const string Conflict = "error.conflict";
    public const string DuplicateContact = "error.duplicateContact";
    public const string AssignmentWarning = "error.assignmentWarning";
    public const string StatusKindChangeWarning = "error.statusKindChangeWarning";
    public const string OpenTasksWarning = "error.openTasksWarning";
    public const string Forbidden = "error.forbidden";
    public const string Unauthorized = "error.unauthorized";
    public const string UnauthorizedDetail = "error.unauthorizedDetail";
    public const string ClientClosedRequest = "error.clientClosedRequest";
    public const string ClientClosedRequestDetail = "error.clientClosedRequestDetail";
    public const string Unexpected = "error.unexpected";
    public const string UnexpectedDetail = "error.unexpectedDetail";
    public const string PasswordChangeRequired = "error.passwordChangeRequired";
    public const string PasswordChangeRequiredDetail = "error.passwordChangeRequiredDetail";

    // Web form field validation (Communication Channels / Web forms) — the only per-field messages
    // in this registry; the field's own bilingual label is prefixed client-side, so these stay generic.
    public const string FieldRequired = "validation.fieldRequired";
    public const string FieldTooLong = "validation.fieldTooLong";
    public const string FieldInvalidFormat = "validation.fieldInvalidFormat";
    public const string FieldInvalidOption = "validation.fieldInvalidOption";
}
