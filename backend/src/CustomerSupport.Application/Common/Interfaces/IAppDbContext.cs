using CustomerSupport.Domain.Ai;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Channels;
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
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Common.Interfaces;

/// <summary>
/// The persistence surface handlers are allowed to touch. Declared in Application so handlers do not
/// reference Infrastructure; implemented by <c>AppDbContext</c>.
/// </summary>
public interface IAppDbContext
{
    // Organization and platform
    DbSet<Branch> Branches { get; }
    DbSet<Department> Departments { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<BrandingSetting> BrandingSettings { get; }

    // Security and administration
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<AgentSkill> AgentSkills { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemSetting> SystemSettings { get; }

    // Customers
    DbSet<Customer> Customers { get; }
    DbSet<CustomerContact> CustomerContacts { get; }
    DbSet<ContactVerification> ContactVerifications { get; }
    DbSet<CustomerNote> CustomerNotes { get; }
    DbSet<Interaction> Interactions { get; }
    DbSet<Attachment> Attachments { get; }

    // Tickets
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketCategory> TicketCategories { get; }
    DbSet<TicketPriority> TicketPriorities { get; }
    DbSet<TicketStatus> TicketStatuses { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<TicketEvent> TicketEvents { get; }
    DbSet<TicketWatcher> TicketWatchers { get; }
    DbSet<Tag> Tags { get; }
    DbSet<TicketTag> TicketTags { get; }

    // Channels
    DbSet<Channel> Channels { get; }
    DbSet<ChannelAccount> ChannelAccounts { get; }
    DbSet<MessageDeliveryLog> MessageDeliveryLogs { get; }
    DbSet<ChatSession> ChatSessions { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<WebFormDefinition> WebFormDefinitions { get; }
    DbSet<WebFormSubmission> WebFormSubmissions { get; }

    // Agent workspace
    DbSet<AgentTask> AgentTasks { get; }
    DbSet<Reminder> Reminders { get; }
    DbSet<QuickReply> QuickReplies { get; }
    DbSet<TicketMention> TicketMentions { get; }

    // SLA and automation
    DbSet<SlaPolicy> SlaPolicies { get; }
    DbSet<SlaTarget> SlaTargets { get; }
    DbSet<SlaPolicyCondition> SlaPolicyConditions { get; }
    DbSet<BusinessCalendar> BusinessCalendars { get; }
    DbSet<BusinessHour> BusinessHours { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<TicketSlaClock> TicketSlaClocks { get; }
    DbSet<AssignmentRule> AssignmentRules { get; }
    DbSet<EscalationRule> EscalationRules { get; }
    DbSet<AutomationRunLog> AutomationRunLogs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }

    // Knowledge base
    DbSet<KbCategory> KbCategories { get; }
    DbSet<KbArticle> KbArticles { get; }
    DbSet<KbArticleVersion> KbArticleVersions { get; }
    DbSet<KbArticleFeedback> KbArticleFeedback { get; }
    DbSet<KbSearchLog> KbSearchLogs { get; }

    // AI
    DbSet<AiSuggestion> AiSuggestions { get; }
    DbSet<AiModelConfig> AiModelConfigs { get; }
    DbSet<ChatbotConversation> ChatbotConversations { get; }
    DbSet<ChatbotMessage> ChatbotMessages { get; }

    // Portal and satisfaction
    DbSet<CsatSurvey> CsatSurveys { get; }

    // Reporting
    DbSet<TicketDailyMetric> TicketDailyMetrics { get; }
    DbSet<ReportDefinition> ReportDefinitions { get; }
    DbSet<ScheduledReport> ScheduledReports { get; }
    DbSet<Dashboard> Dashboards { get; }
    DbSet<DashboardWidget> DashboardWidgets { get; }

    // Integrations
    DbSet<ApiClient> ApiClients { get; }
    DbSet<IntegrationConnection> IntegrationConnections { get; }
    DbSet<IntegrationSyncLog> IntegrationSyncLogs { get; }
    DbSet<Webhook> Webhooks { get; }
    DbSet<WebhookDelivery> WebhookDeliveries { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
