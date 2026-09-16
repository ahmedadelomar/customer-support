using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Sla;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Channel = CustomerSupport.Domain.Channels.Channel;

namespace CustomerSupport.Infrastructure.Persistence.Seed;

/// <summary>
/// Brings a fresh database to a usable state: permissions, roles, lookups, a default branch,
/// a business calendar and an SLA policy. Idempotent, so it is safe to run on every startup.
/// </summary>
public class DbSeeder(
    AppDbContext db,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    ILogger<DbSeeder> logger)
{
    /// <summary>Roles shipped with the product, mapped to the permission categories they cover.</summary>
    private static readonly (string Name, string NameAr, string[] Categories)[] SystemRoles =
    [
        ("SystemAdministrator", "مدير النظام", ["*"]),
        ("SupportManager", "مدير الدعم",
            ["Customers", "Tickets", "Channels", "Workspace", "Sla", "KnowledgeBase", "Ai", "Reports"]),
        ("TeamLeader", "رئيس الفريق",
            ["Customers", "Tickets", "Workspace", "KnowledgeBase", "Ai", "Reports"]),
        ("Agent", "موظف الدعم", ["Customers", "Tickets", "Workspace", "KnowledgeBase", "Ai"]),
        ("KnowledgeAuthor", "محرر المعرفة", ["KnowledgeBase"]),
        ("Viewer", "مشاهد", []),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedSystemSettingsAsync(ct);
        await SeedRolesAsync(ct);
        var branchId = await SeedBranchAndDepartmentsAsync(ct);
        await SeedTicketLookupsAsync(ct);
        await SeedChannelsAsync(ct);
        await SeedSlaAsync(branchId, ct);
        await SeedAdminUserAsync(branchId, ct);

        logger.LogInformation("Database seeding completed.");
    }

    /// <summary>Upserts the permission table from the code registry so the two cannot drift.</summary>
    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Key).ToListAsync(ct);
        var missing = Permissions.All.Where(p => !existing.Contains(p.Key)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.Permissions.AddRange(missing.Select(p => new Permission
        {
            Key = p.Key,
            Category = p.Category,
            Name = new LocalizedText(Humanize(p.Key), Humanize(p.Key)),
            IsSystem = true,
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} permission(s).", missing.Count);
    }

    /// <summary>
    /// Inserts only the global rows that do not exist yet, so an administrator's edits survive every
    /// restart. <c>SettingKeys</c> is the single source for keys, types and defaults.
    /// </summary>
    private async Task SeedSystemSettingsAsync(CancellationToken ct)
    {
        var existing = await db.SystemSettings
            .Where(s => s.BranchId == null)
            .Select(s => s.Key)
            .ToListAsync(ct);

        var missing = SettingKeys.All.Where(d => !existing.Contains(d.Key)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.SystemSettings.AddRange(missing.Select(d => new SystemSetting
        {
            Key = d.Key,
            Value = d.DefaultValue,
            DataType = d.DataType,
            Category = d.Category,
            Name = new LocalizedText(d.NameEn, d.NameAr),
            Description = d.DescriptionEn,
            IsSecret = d.IsSecret,
            IsSystem = true,
        }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} system setting(s).", missing.Count);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var allPermissions = await db.Permissions.ToListAsync(ct);

        foreach (var (name, nameAr, categories) in SystemRoles)
        {
            var role = await roleManager.FindByNameAsync(name);
            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    DisplayName = new LocalizedText(Humanize(name), nameAr),
                    IsSystem = true,
                };

                var created = await roleManager.CreateAsync(role);
                if (!created.Succeeded)
                {
                    logger.LogError(
                        "Failed to create role {Role}: {Errors}",
                        name,
                        string.Join("; ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            // Grant only what is missing, so an administrator's manual revocations survive a restart.
            var granted = await db.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(ct);

            var target = categories.Contains("*")
                ? allPermissions
                : allPermissions.Where(p => categories.Contains(p.Category)).ToList();

            var toGrant = target.Where(p => !granted.Contains(p.Id)).ToList();
            if (toGrant.Count == 0)
            {
                continue;
            }

            db.RolePermissions.AddRange(toGrant.Select(p => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = p.Id,
                GrantedAt = DateTimeOffset.UtcNow,
            }));

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<Guid> SeedBranchAndDepartmentsAsync(CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Code == "HQ", ct);
        if (branch is null)
        {
            branch = new Branch
            {
                Code = "HQ",
                Name = new LocalizedText("Head Office", "المركز الرئيسي"),
                TimeZoneId = "Asia/Riyadh",
                IsActive = true,
            };

            db.Branches.Add(branch);
            await db.SaveChangesAsync(ct);
        }

        (string Code, string En, string Ar)[] departments =
        [
            ("GEN", "General Support", "الدعم العام"),
            ("TECH", "Technical Support", "الدعم الفني"),
            ("BILL", "Billing", "الفواتير"),
            ("SALES", "Sales", "المبيعات"),
        ];

        foreach (var (code, en, ar) in departments)
        {
            if (await db.Departments.AnyAsync(d => d.Code == code && d.BranchId == branch.Id, ct))
            {
                continue;
            }

            db.Departments.Add(new Department
            {
                BranchId = branch.Id,
                Code = code,
                Name = new LocalizedText(en, ar),
                IsActive = true,
            });
        }

        await db.SaveChangesAsync(ct);
        return branch.Id;
    }

    private async Task SeedTicketLookupsAsync(CancellationToken ct)
    {
        if (!await db.TicketPriorities.AnyAsync(ct))
        {
            db.TicketPriorities.AddRange(
                new TicketPriority { Code = "low", Name = new LocalizedText("Low", "منخفضة"), Level = 1, ColorHex = "#64748B" },
                new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادية"), Level = 2, ColorHex = "#0E7490", IsDefault = true },
                new TicketPriority { Code = "high", Name = new LocalizedText("High", "عالية"), Level = 3, ColorHex = "#B45309" },
                new TicketPriority { Code = "urgent", Name = new LocalizedText("Urgent", "عاجلة"), Level = 4, ColorHex = "#BE123C" });
        }

        if (!await db.TicketStatuses.AnyAsync(ct))
        {
            db.TicketStatuses.AddRange(
                new TicketStatus { Code = "new", Name = new LocalizedText("New", "جديدة"), Kind = TicketStatusKind.New, ColorHex = "#1D4ED8", DisplayOrder = 1, IsDefault = true },
                new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوحة"), Kind = TicketStatusKind.Open, ColorHex = "#0E7490", DisplayOrder = 2 },
                new TicketStatus { Code = "pending-customer", Name = new LocalizedText("Pending Customer", "بانتظار العميل"), Kind = TicketStatusKind.Pending, ColorHex = "#B45309", DisplayOrder = 3, PausesSla = true },
                new TicketStatus { Code = "on-hold", Name = new LocalizedText("On Hold", "معلقة"), Kind = TicketStatusKind.OnHold, ColorHex = "#7C3AED", DisplayOrder = 4, PausesSla = true },
                new TicketStatus { Code = "resolved", Name = new LocalizedText("Resolved", "تم الحل"), Kind = TicketStatusKind.Resolved, ColorHex = "#047857", DisplayOrder = 5 },
                new TicketStatus { Code = "closed", Name = new LocalizedText("Closed", "مغلقة"), Kind = TicketStatusKind.Closed, ColorHex = "#475569", DisplayOrder = 6, IsTerminal = true },
                new TicketStatus { Code = "cancelled", Name = new LocalizedText("Cancelled", "ملغاة"), Kind = TicketStatusKind.Cancelled, ColorHex = "#9F1239", DisplayOrder = 7, IsTerminal = true, IsVisibleInPortal = false });
        }

        if (!await db.TicketCategories.AnyAsync(ct))
        {
            db.TicketCategories.AddRange(
                new TicketCategory { Code = "general-inquiry", Name = new LocalizedText("General Inquiry", "استفسار عام"), Path = "/general-inquiry/", DisplayOrder = 1 },
                new TicketCategory { Code = "technical-issue", Name = new LocalizedText("Technical Issue", "مشكلة فنية"), Path = "/technical-issue/", DisplayOrder = 2 },
                new TicketCategory { Code = "billing", Name = new LocalizedText("Billing and Payments", "الفواتير والمدفوعات"), Path = "/billing/", DisplayOrder = 3 },
                new TicketCategory { Code = "complaint", Name = new LocalizedText("Complaint", "شكوى"), Path = "/complaint/", DisplayOrder = 4 },
                new TicketCategory { Code = "feature-request", Name = new LocalizedText("Feature Request", "طلب ميزة"), Path = "/feature-request/", DisplayOrder = 5 });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedChannelsAsync(CancellationToken ct)
    {
        if (await db.Channels.AnyAsync(ct))
        {
            return;
        }

        db.Channels.AddRange(
            new Channel { Key = ChannelKey.Email, Name = new LocalizedText("Email", "البريد الإلكتروني"), Icon = "mail", DisplayOrder = 1 },
            new Channel { Key = ChannelKey.WhatsApp, Name = new LocalizedText("WhatsApp", "واتساب"), Icon = "whatsapp", DisplayOrder = 2 },
            new Channel { Key = ChannelKey.LiveChat, Name = new LocalizedText("Live Chat", "المحادثة المباشرة"), Icon = "chat", DisplayOrder = 3 },
            new Channel { Key = ChannelKey.Sms, Name = new LocalizedText("SMS", "الرسائل النصية"), Icon = "sms", DisplayOrder = 4, SupportsAttachments = false },
            new Channel { Key = ChannelKey.WebForm, Name = new LocalizedText("Web Form", "نموذج الويب"), Icon = "form", DisplayOrder = 5, SupportsOutbound = false },
            new Channel { Key = ChannelKey.Portal, Name = new LocalizedText("Customer Portal", "بوابة العملاء"), Icon = "portal", DisplayOrder = 6 },
            new Channel { Key = ChannelKey.Phone, Name = new LocalizedText("Phone", "الهاتف"), Icon = "phone", DisplayOrder = 7, SupportsAttachments = false },
            new Channel { Key = ChannelKey.Api, Name = new LocalizedText("API", "واجهة برمجية"), Icon = "api", DisplayOrder = 8 },
            new Channel { Key = ChannelKey.Internal, Name = new LocalizedText("Internal", "داخلي"), Icon = "internal", DisplayOrder = 9 });

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSlaAsync(Guid branchId, CancellationToken ct)
    {
        var calendar = await db.BusinessCalendars.FirstOrDefaultAsync(c => c.IsDefault, ct);
        if (calendar is null)
        {
            calendar = new BusinessCalendar
            {
                BranchId = branchId,
                Name = new LocalizedText("Default Working Hours", "ساعات العمل الافتراضية"),
                TimeZoneId = "Asia/Riyadh",
                IsDefault = true,
            };

            // Sunday to Thursday, 08:00 to 17:00 — the standard Saudi working week.
            foreach (var day in new[]
                     {
                         DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
                         DayOfWeek.Wednesday, DayOfWeek.Thursday,
                     })
            {
                calendar.BusinessHours.Add(new BusinessHour
                {
                    DayOfWeek = day,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(17, 0),
                });
            }

            db.BusinessCalendars.Add(calendar);
            await db.SaveChangesAsync(ct);
        }

        if (await db.SlaPolicies.AnyAsync(p => p.IsDefault, ct))
        {
            return;
        }

        var policy = new SlaPolicy
        {
            BranchId = branchId,
            Name = new LocalizedText("Standard SLA", "اتفاقية الخدمة القياسية"),
            BusinessCalendarId = calendar.Id,
            EvaluationOrder = 100,
            IsDefault = true,
            WarningThresholdPercent = 80,
        };

        var priorities = await db.TicketPriorities.ToListAsync(ct);

        // Response and resolution budgets in working minutes, tightening as priority rises.
        var budgets = new Dictionary<string, (int FirstResponse, int Resolution)>
        {
            ["low"] = (480, 4800),
            ["normal"] = (240, 2400),
            ["high"] = (60, 480),
            ["urgent"] = (15, 240),
        };

        foreach (var priority in priorities)
        {
            if (!budgets.TryGetValue(priority.Code, out var budget))
            {
                continue;
            }

            policy.Targets.Add(new SlaTarget
            {
                PriorityId = priority.Id,
                FirstResponseMinutes = budget.FirstResponse,
                ResolutionMinutes = budget.Resolution,
            });
        }

        db.SlaPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates the bootstrap administrator only when no agent exists. The password must be supplied
    /// through configuration; a hard-coded default would ship a known credential.
    /// </summary>
    private async Task SeedAdminUserAsync(Guid branchId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.UserType == UserType.Agent, ct))
        {
            return;
        }

        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No agent users exist and SEED_ADMIN_PASSWORD is not set, so the bootstrap administrator was not created. "
                + "Set SEED_ADMIN_PASSWORD and restart to create it.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = "admin",
            Email = Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL") ?? "admin@localhost",
            EmailConfirmed = true,
            UserType = UserType.Agent,
            DisplayName = new LocalizedText("System Administrator", "مدير النظام"),
            BranchId = branchId,
            PreferredLanguage = "en",
            IsActive = true,
            MustChangePassword = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to create the bootstrap administrator: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, "SystemAdministrator");
        logger.LogInformation("Created the bootstrap administrator. It must change its password on first sign-in.");
    }

    /// <summary>Turns a permission key or role name into a readable label for the seeded display name.</summary>
    private static string Humanize(string value)
    {
        var text = value.Replace('.', ' ').Replace('_', ' ');
        var spaced = string.Concat(text.Select((ch, i) =>
            i > 0 && char.IsUpper(ch) && !char.IsUpper(text[i - 1]) ? $" {ch}" : ch.ToString()));

        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
