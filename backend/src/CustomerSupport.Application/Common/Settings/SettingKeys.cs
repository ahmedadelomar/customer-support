namespace CustomerSupport.Application.Common.Settings;

/// <summary>One configurable setting: its key, type, category and compiled-in default.</summary>
/// <remarks>
/// The default lives here, not only in the seeded row, so a missing or deleted row still resolves to
/// a usable value. Code should never have to null-check configuration.
/// </remarks>
public record SettingDefinition(
    string Key,
    string DataType,
    string Category,
    string DefaultValue,
    string NameEn,
    string NameAr,
    string? DescriptionEn = null,
    string? DescriptionAr = null,
    bool IsSecret = false);

/// <summary>
/// The single source of truth for runtime settings (Security &amp; Administration / System
/// configuration). The seeder inserts from this list and <c>ISettingsProvider</c> falls back to it,
/// so the table and the code defaults cannot disagree.
/// </summary>
public static class SettingKeys
{
    public const string AuditRetentionDays = "audit.retentionDays";
    public const string TicketsNumberPrefix = "tickets.numberPrefix";
    public const string TicketsAutoCloseResolvedAfterDays = "tickets.autoCloseResolvedAfterDays";
    public const string SlaWarningThresholdPercent = "sla.warningThresholdPercent";
    public const string AttachmentsMaxBytes = "attachments.maxBytes";
    public const string AttachmentsAllowedExtensions = "attachments.allowedExtensions";
    public const string PortalEnabled = "portal.enabled";
    public const string AiEnabled = "ai.enabled";
    public const string CsatEnabled = "csat.enabled";
    public const string CsatSurveyExpiryDays = "csat.surveyExpiryDays";
    public const string LiveChatMaxConcurrentSessions = "livechat.maxConcurrentSessions";
    public const string LiveChatAbandonTimeoutMinutes = "livechat.abandonTimeoutMinutes";
    public const string LiveChatOfflineFallbackSeconds = "livechat.offlineFallbackSeconds";
    public const string SmsMaxSegments = "sms.maxSegments";
    public const string SmsCostPerSegment = "sms.costPerSegment";

    /// <summary>
    /// Settings safe to expose without authentication, for the portal's pre-sign-in shell. Anything
    /// not on this list is never returned by the public endpoint, whatever its category.
    /// </summary>
    public static readonly IReadOnlySet<string> PublicKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        PortalEnabled,
    };

    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new(AuditRetentionDays, "int", "Audit", "400",
            "Audit retention (days)", "مدة حفظ سجل التدقيق (أيام)",
            "How long audit entries are kept. 0 keeps them forever.",
            "مدة الاحتفاظ بسجلات التدقيق. القيمة 0 تعني الاحتفاظ دائمًا."),

        new(TicketsNumberPrefix, "string", "Tickets", "TCK",
            "Ticket number prefix", "بادئة رقم التذكرة",
            "Prefix used when generating ticket reference numbers.",
            "البادئة المستخدمة عند توليد أرقام التذاكر."),

        new(TicketsAutoCloseResolvedAfterDays, "int", "Tickets", "7",
            "Auto-close resolved after (days)", "الإغلاق التلقائي بعد الحل (أيام)",
            "Resolved tickets with no further reply are closed after this many days.",
            "تُغلق التذاكر المحلولة دون رد إضافي بعد هذا العدد من الأيام."),

        new(SlaWarningThresholdPercent, "int", "Sla", "80",
            "SLA warning threshold (%)", "حد تحذير اتفاقية الخدمة (٪)",
            "Percentage of the SLA target at which a ticket is flagged as approaching breach.",
            "النسبة من هدف اتفاقية الخدمة التي تُعلَّم عندها التذكرة كقريبة من الإخلال."),

        new(AttachmentsMaxBytes, "int", "Attachments", "26214400",
            "Maximum attachment size (bytes)", "الحد الأقصى لحجم المرفق (بايت)",
            "Uploads larger than this are rejected.",
            "تُرفض الملفات التي تتجاوز هذا الحجم."),

        new(AttachmentsAllowedExtensions, "string", "Attachments", "pdf,png,jpg,jpeg,docx,xlsx,csv,txt",
            "Allowed attachment types", "أنواع المرفقات المسموح بها",
            "Comma-separated list of permitted file extensions.",
            "قائمة بالامتدادات المسموح بها مفصولة بفواصل."),

        new(PortalEnabled, "bool", "Portal", "true",
            "Customer portal enabled", "تفعيل بوابة العملاء",
            "Turns the customer self-service portal on or off.",
            "تشغيل أو إيقاف بوابة الخدمة الذاتية للعملاء."),

        new(AiEnabled, "bool", "Ai", "false",
            "AI features enabled", "تفعيل ميزات الذكاء الاصطناعي",
            "Master switch for every AI-assisted feature.",
            "مفتاح رئيسي لكل الميزات المدعومة بالذكاء الاصطناعي."),

        new(CsatEnabled, "bool", "Csat", "true",
            "Satisfaction surveys enabled", "تفعيل استبيانات الرضا",
            "Sends a satisfaction survey when a ticket is resolved.",
            "إرسال استبيان رضا عند حل التذكرة."),

        new(CsatSurveyExpiryDays, "int", "Csat", "14",
            "Survey expiry (days)", "انتهاء صلاحية الاستبيان (أيام)",
            "How long a satisfaction survey link stays valid.",
            "المدة التي يبقى فيها رابط الاستبيان صالحًا."),

        new(LiveChatMaxConcurrentSessions, "int", "LiveChat", "3",
            "Max concurrent chats per agent", "الحد الأقصى للمحادثات المتزامنة لكل وكيل",
            "An agent cannot accept another chat once this many are active.",
            "لا يمكن للوكيل قبول محادثة أخرى بعد الوصول لهذا العدد من المحادثات النشطة."),

        new(LiveChatAbandonTimeoutMinutes, "int", "LiveChat", "10",
            "Abandon chat after inactivity (minutes)", "اعتبار المحادثة متروكة بعد عدم النشاط (دقائق)",
            "A waiting or active chat with no activity for this long is marked abandoned.",
            "تُعتبر المحادثة المنتظرة أو النشطة متروكة بعد هذه المدة دون أي نشاط."),

        new(LiveChatOfflineFallbackSeconds, "int", "LiveChat", "60",
            "Offline fallback after (seconds)", "التحول لوضع عدم التوفر بعد (ثوانٍ)",
            "The widget offers the offline message form after waiting this long with no agent.",
            "تعرض الأداة نموذج ترك رسالة بعد هذه المدة من الانتظار دون توفر وكيل."),

        new(SmsMaxSegments, "int", "Sms", "3",
            "Maximum SMS segments per message", "الحد الأقصى لعدد أجزاء الرسالة النصية",
            "A reply that would split into more segments than this is blocked before sending.",
            "يُمنع إرسال أي رد يتجاوز هذا العدد من الأجزاء."),

        new(SmsCostPerSegment, "string", "Sms", "0.02",
            "Estimated cost per SMS segment", "التكلفة التقديرية لكل جزء من الرسالة النصية",
            "Used only to estimate reporting cost, in the account's billing currency — not the provider's actual invoice.",
            "تُستخدم فقط لتقدير التكلفة في التقارير بعملة الفوترة الخاصة بالحساب — وليست فاتورة المزود الفعلية."),
    ];

    public static SettingDefinition? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));
}
