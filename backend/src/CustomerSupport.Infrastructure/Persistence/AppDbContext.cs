using System.Linq.Expressions;
using System.Reflection;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Ai;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Files;
using CustomerSupport.Domain.Identity;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Domain.KnowledgeBase;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Portal;
using CustomerSupport.Domain.Reporting;
using CustomerSupport.Domain.Sla;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Domain.Workspace;
using CustomerSupport.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CustomerSupport.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context. It extends the Identity context so users, roles and domain data share
/// one transaction, which the permission and audit-trail flows depend on.
/// </summary>
/// <remarks>
/// Branch scoping is deliberately NOT a global query filter. A filter would silently hide rows from
/// background jobs and cross-branch reports, which both legitimately need the whole table. Handlers
/// opt in explicitly with <c>WhereBranchAccessible</c> instead.
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IAppDbContext
{
    // Organization and platform
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<BrandingSetting> BrandingSettings => Set<BrandingSetting>();

    // Security and administration
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AgentSkill> AgentSkills => Set<AgentSkill>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Customers
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<ContactVerification> ContactVerifications => Set<ContactVerification>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    // Tickets
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<TicketWatcher> TicketWatchers => Set<TicketWatcher>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();

    // Channels
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelAccount> ChannelAccounts => Set<ChannelAccount>();
    public DbSet<MessageDeliveryLog> MessageDeliveryLogs => Set<MessageDeliveryLog>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<WebFormDefinition> WebFormDefinitions => Set<WebFormDefinition>();
    public DbSet<WebFormSubmission> WebFormSubmissions => Set<WebFormSubmission>();

    // Agent workspace
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();
    public DbSet<TicketMention> TicketMentions => Set<TicketMention>();

    // SLA and automation
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
    public DbSet<SlaPolicyCondition> SlaPolicyConditions => Set<SlaPolicyCondition>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<TicketSlaClock> TicketSlaClocks => Set<TicketSlaClock>();
    public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<AutomationRunLog> AutomationRunLogs => Set<AutomationRunLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    // Knowledge base
    public DbSet<KbCategory> KbCategories => Set<KbCategory>();
    public DbSet<KbArticle> KbArticles => Set<KbArticle>();
    public DbSet<KbArticleVersion> KbArticleVersions => Set<KbArticleVersion>();
    public DbSet<KbArticleFeedback> KbArticleFeedback => Set<KbArticleFeedback>();
    public DbSet<KbSearchLog> KbSearchLogs => Set<KbSearchLog>();

    // AI
    public DbSet<AiSuggestion> AiSuggestions => Set<AiSuggestion>();
    public DbSet<AiModelConfig> AiModelConfigs => Set<AiModelConfig>();
    public DbSet<ChatbotConversation> ChatbotConversations => Set<ChatbotConversation>();
    public DbSet<ChatbotMessage> ChatbotMessages => Set<ChatbotMessage>();

    // Portal and satisfaction
    public DbSet<CsatSurvey> CsatSurveys => Set<CsatSurvey>();

    // Reporting
    public DbSet<TicketDailyMetric> TicketDailyMetrics => Set<TicketDailyMetric>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();

    // Integrations
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationSyncLog> IntegrationSyncLogs => Set<IntegrationSyncLog>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyLocalizedTextConvention(builder);
        ApplySoftDeleteFilters(builder);
        ApplyDecimalPrecision(builder);
        RenameIdentityTables(builder);

        if (Database.IsSqlite())
        {
            ApplySqliteConversions(builder);
        }
    }

    /// <summary>
    /// SQLite has no native <see cref="DateTimeOffset"/> or <see cref="decimal"/> type, and refuses to
    /// ORDER BY either. Converting them keeps the development database usable without changing the
    /// SQL Server mapping, which is the production target.
    /// </summary>
    /// <remarks>
    /// <c>DateTimeOffsetToBinaryConverter</c> is EF's recommended choice here: it packs the value into
    /// a long that still sorts chronologically, so paging and "newest first" keep working.
    /// </remarks>
    private static void ApplySqliteConversions(ModelBuilder builder)
    {
        // Converters are declared on the NON-nullable type even for nullable properties: EF wraps
        // them and handles null itself. Declaring a ValueConverter<T?, U?> makes EF apply its own
        // null handling on top and throws "Nullable object must have a value" while materialising.
        var dateTimeOffset = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        var decimalToDouble = new ValueConverter<decimal, double>(
            v => (double)v,
            v => (decimal)v);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (type == typeof(DateTimeOffset))
                {
                    // Unix milliseconds rather than DateTimeOffsetToBinaryConverter: the binary form
                    // sorts by local ticks, so it would order incorrectly across mixed offsets.
                    // Everything here is written as UTC, and this form sorts by absolute time.
                    property.SetValueConverter(dateTimeOffset);
                }
                else if (type == typeof(decimal))
                {
                    property.SetValueConverter(decimalToDouble);
                }
            }
        }
    }

    /// <summary>
    /// Maps every <see cref="LocalizedText"/> property to a pair of columns named after the property:
    /// <c>Name</c> becomes <c>NameEn</c> and <c>NameAr</c>. Applying this by convention avoids
    /// repeating an <c>OwnsOne</c> block on roughly forty entities and guarantees the naming is uniform.
    /// </summary>
    private static void ApplyLocalizedTextConvention(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().ToList())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var properties = entityType.ClrType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(LocalizedText) && p.CanWrite)
                .ToList();

            foreach (var property in properties)
            {
                builder.Entity(entityType.ClrType).OwnsOne(
                    typeof(LocalizedText),
                    property.Name,
                    owned =>
                    {
                        owned.Property(nameof(LocalizedText.En))
                            .HasColumnName($"{property.Name}En")
                            .HasMaxLength(1000)
                            .IsRequired();

                        owned.Property(nameof(LocalizedText.Ar))
                            .HasColumnName($"{property.Name}Ar")
                            .HasMaxLength(1000)
                            .IsRequired();
                    });
            }
        }
    }

    /// <summary>
    /// Adds <c>WHERE IsDeleted = 0</c> to every soft-deletable entity, so no handler can accidentally
    /// return deleted rows.
    /// </summary>
    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>SQL Server truncates decimals silently without explicit precision, so set it everywhere.</summary>
    private static void ApplyDecimalPrecision(ModelBuilder builder)
    {
        var decimalProperties = builder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

        foreach (var property in decimalProperties)
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }
    }

    /// <summary>Drops the <c>AspNet</c> prefix so the Identity tables match the rest of the schema.</summary>
    private static void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
    }

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        base.SaveChangesAsync(cancellationToken);
}
