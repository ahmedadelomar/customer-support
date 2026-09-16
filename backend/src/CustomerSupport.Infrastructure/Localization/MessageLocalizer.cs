using System.Globalization;
using CustomerSupport.Application.Common.Localization;

namespace CustomerSupport.Infrastructure.Localization;

/// <summary>
/// Dictionary-backed <see cref="IMessageLocalizer"/> (Platform / Bilingual UI and data).
/// </summary>
/// <remarks>
/// Held in code rather than in `.resx` files on purpose: the set is small, both languages sit side by
/// side where a reviewer can see a missing translation, and it needs no satellite assemblies or
/// designer-generated partial classes. If this grows past a few dozen entries, move it to resources.
/// </remarks>
public class MessageLocalizer : IMessageLocalizer
{
    private const string DefaultCulture = "ar";

    private static readonly Dictionary<string, (string Ar, string En)> Messages = new(StringComparer.Ordinal)
    {
        [MessageKeys.ValidationFailed] = ("فشل التحقق من البيانات", "Validation failed"),
        [MessageKeys.NotFound] = ("العنصر غير موجود", "Resource not found"),
        [MessageKeys.Conflict] = ("تعارض", "Conflict"),
        [MessageKeys.DuplicateContact] = ("وسيلة تواصل مكررة", "Duplicate contact"),
        [MessageKeys.AssignmentWarning] = ("تحذير عند الإسناد", "Assignment warning"),
        [MessageKeys.StatusKindChangeWarning] = ("تحذير تغيير نوع الحالة", "Status kind change warning"),
        [MessageKeys.OpenTasksWarning] = ("تحذير وجود مهام مفتوحة", "Open tasks warning"),
        [MessageKeys.Forbidden] = ("غير مصرح", "Forbidden"),
        [MessageKeys.Unauthorized] = ("مطلوب تسجيل الدخول", "Unauthorized"),
        [MessageKeys.UnauthorizedDetail] = ("يلزم تسجيل الدخول للمتابعة.", "Authentication is required."),
        [MessageKeys.ClientClosedRequest] = ("أُلغي الطلب", "Client closed request"),
        [MessageKeys.ClientClosedRequestDetail] = ("تم إلغاء الطلب.", "The request was cancelled."),
        [MessageKeys.Unexpected] = ("خطأ غير متوقع", "Unexpected error"),
        [MessageKeys.UnexpectedDetail] = (
            "حدث خطأ غير متوقع. يرجى ذكر معرّف التتبع عند الإبلاغ عن هذه المشكلة.",
            "An unexpected error occurred. Reference the trace id when reporting this."),
        [MessageKeys.PasswordChangeRequired] = ("مطلوب تغيير كلمة المرور", "Password change required"),
        [MessageKeys.PasswordChangeRequiredDetail] = (
            "يجب تغيير كلمة المرور قبل استخدام التطبيق.",
            "You must change your password before using the application."),
    };

    public string this[string key] => For(key, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public string For(string key, string culture)
    {
        if (!Messages.TryGetValue(key, out var pair))
        {
            // Returning the key makes a missing translation obvious in the response rather than
            // silently blank, which is the same trade-off the client's translate pipe makes.
            return key;
        }

        return culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? pair.Ar : pair.En;
    }

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, this[key], args);

    /// <summary>The culture used when the request carries none. Arabic is the product default.</summary>
    public static string FallbackCulture => DefaultCulture;
}
