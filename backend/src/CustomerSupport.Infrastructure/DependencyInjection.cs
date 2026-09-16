using CustomerSupport.Application.Automation;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Channels.Inbound;
using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Channels.Outbound;
using CustomerSupport.Application.Channels.Sms;
using CustomerSupport.Application.Channels.WebForms;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Localization;
using CustomerSupport.Application.Files;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Application.Workspace.QuickReplies;
using CustomerSupport.Infrastructure.Channels;
using CustomerSupport.Infrastructure.Channels.Email;
using CustomerSupport.Infrastructure.Channels.Sms;
using CustomerSupport.Infrastructure.Channels.WebForms;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Localization;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Persistence.Interceptors;
using CustomerSupport.Infrastructure.Persistence.Seed;
using CustomerSupport.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace CustomerSupport.Infrastructure;

/// <summary>Wires persistence, Identity and the infrastructure service implementations.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // SqlServer is the production target. Sqlite exists so a developer can clone and run with no
        // database server installed; it is not a supported deployment target.
        var provider = configuration.GetValue("Database:Provider", "SqlServer");

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<NotificationRealtimeInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString, sqlite =>
                    sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                    // Transient SQL failures are common against managed instances; retry rather than 500.
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null);
                });
            }

            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<AuditLogInterceptor>(),
                sp.GetRequiredService<NotificationRealtimeInterceptor>());
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IMessageLocalizer, MessageLocalizer>();
        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
        services.AddScoped<ITicketEventRecorder, TicketEventRecorder>();
        services.AddScoped<IInteractionRecorder, InteractionRecorder>();
        services.AddScoped<IUserDisplayNameResolver, UserDisplayNameResolver>();
        services.AddScoped<IAgentDirectory, AgentDirectory>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IAgentCapacityService, AgentCapacityService>();
        services.AddScoped<IPlaceholderResolver, PlaceholderResolver>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IContactVerificationSender, LoggingContactVerificationSender>();
        services.AddScoped<IAttachmentOwnerAuthorizer, AttachmentOwnerAuthorizer>();

        // SLA and Automation / Response and resolution targets (CS-501). IConditionEvaluator is the
        // one allow-listed field map shared with automatic assignment (CS-502) and escalation
        // (CS-503) rule evaluation, so a field either works everywhere or is rejected everywhere.
        services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
        services.AddScoped<IBusinessCalendarCalculator, BusinessCalendarCalculator>();
        services.AddScoped<IBusinessCalendarCacheInvalidator, BusinessCalendarCacheInvalidator>();
        services.AddScoped<ISlaEngine, SlaEngine>();

        // SLA and Automation / Automatic assignment (CS-502). IRuleEvaluator is also reused, as-is,
        // by escalation rule evaluation (CS-503).
        services.AddScoped<IRuleEvaluator, RuleEvaluator>();
        services.AddScoped<IAssignmentEngine, AssignmentEngine>();

        // SLA and Automation / Escalation rules (CS-503).
        services.AddScoped<IEscalationEngine, EscalationEngine>();

        // SLA and Automation / Alerts and notifications (CS-504). The real email/SMS/push senders
        // are CS-301/302/304's job; this logs instead until those land.
        services.AddScoped<IExternalNotificationSender, LoggingExternalNotificationSender>();
        services.AddScoped<IOutboxMessageHandler, NotificationOutboxHandler>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Communication Channels / Email channel (CS-301). IInboundMessagePipeline is built general
        // enough for CS-302 (WhatsApp) and CS-304 (SMS) to reuse unchanged.
        services.AddScoped<IInboundMessagePipeline, InboundMessagePipeline>();
        services.AddScoped<IChannelWebhookSecrets, ChannelWebhookSecrets>();
        services.AddScoped<IEmailChannelSender, LoggingEmailChannelSender>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<IOutboxMessageHandler, EmailChannelOutboxHandler>();
        services.AddSingleton<IVirusScanner, NoOpVirusScanner>();

        // Communication Channels / Live chat (CS-303). IChatRealtimeNotifier is registered in the Api
        // project instead — it needs IHubContext<ChatHub>, an Api-layer type.
        services.AddScoped<IChatVisitorTokenService, ChatVisitorTokenService>();

        // Communication Channels / Web forms (CS-305). IWebFormValidator is Singleton so its compiled
        // regex cache survives across requests instead of rebuilding per submission.
        services.AddSingleton<IWebFormValidator, WebFormValidator>();
        services.AddScoped<IWebFormTicketFactory, WebFormTicketFactory>();
        services.AddScoped<IWebFormSubmissionRetryService, WebFormSubmissionRetryService>();
        services.AddScoped<ICaptchaVerifier, LoggingCaptchaVerifier>();

        // Communication Channels / SMS channel (CS-304). Reuses IInboundMessagePipeline (CS-301)
        // unchanged, adding only its own outbound sender and outbox handler.
        services.AddScoped<ISmsChannelSender, LoggingSmsChannelSender>();
        services.AddScoped<IOutboxMessageHandler, SmsChannelOutboxHandler>();

        // Runtime configuration (Security & Administration / System configuration). These three read
        // through ISettingsProvider, which holds a DbContext, so they are scoped rather than
        // singletons — a singleton depending on a scoped service is a captive dependency.
        services.AddMemoryCache();
        services.AddScoped<ISettingsProvider, SettingsProvider>();
        services.AddScoped<IAttachmentPolicyProvider, AttachmentPolicyProvider>();
        services.AddScoped<IAutoCloseSettingsProvider, AutoCloseSettingsProvider>();
        services.AddScoped<IAuditRetentionSettings, AuditRetentionSettings>();
        services.AddScoped<DbSeeder>();

        // Hourly auto-close sweep (Ticket Management / Status workflow and escalation). The package
        // was already referenced before any job used it; this is the first thing to actually run on it.
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey(nameof(AutoCloseResolvedTicketsJob));
            q.AddJob<AutoCloseResolvedTicketsJob>(opts => opts.WithIdentity(jobKey));
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{nameof(AutoCloseResolvedTicketsJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));

            // Reminder dispatch (Agent Dashboard / Tasks and reminders) — every minute.
            // [DisallowConcurrentExecution] on the job itself is the second layer against a double
            // send; the claim-before-dispatch update inside it is the first and the one that matters
            // across multiple app instances, which Quartz's in-process lock does not cover.
            var reminderJobKey = new JobKey(nameof(ReminderDispatchJob));
            q.AddJob<ReminderDispatchJob>(opts => opts.WithIdentity(reminderJobKey));
            q.AddTrigger(opts => opts
                .ForJob(reminderJobKey)
                .WithIdentity($"{nameof(ReminderDispatchJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(1).RepeatForever()));

            // Audit retention (Security & Administration / Audit logs) — nightly at 03:00, when the
            // batched deletes are least likely to compete with agents for the database.
            var auditJobKey = new JobKey(nameof(AuditRetentionJob));
            q.AddJob<AuditRetentionJob>(opts => opts.WithIdentity(auditJobKey));
            q.AddTrigger(opts => opts
                .ForJob(auditJobKey)
                .WithIdentity($"{nameof(AuditRetentionJob)}-trigger")
                .WithCronSchedule("0 0 3 * * ?"));

            // SLA breach sweep (SLA and Automation / Response and resolution targets) — every
            // minute, so a breach is detected by a background pass rather than only when someone
            // opens the ticket.
            var slaSweepJobKey = new JobKey(nameof(SlaBreachSweepJob));
            q.AddJob<SlaBreachSweepJob>(opts => opts.WithIdentity(slaSweepJobKey));
            q.AddTrigger(opts => opts
                .ForJob(slaSweepJobKey)
                .WithIdentity($"{nameof(SlaBreachSweepJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(1).RepeatForever()));

            // Escalation evaluation (SLA and Automation / Escalation rules) — every 5 minutes, so
            // at-risk tickets escalate whether or not anyone has the ticket open.
            var escalationJobKey = new JobKey(nameof(EscalationEvaluationJob));
            q.AddJob<EscalationEvaluationJob>(opts => opts.WithIdentity(escalationJobKey));
            q.AddTrigger(opts => opts
                .ForJob(escalationJobKey)
                .WithIdentity($"{nameof(EscalationEvaluationJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

            // Outbox dispatch (SLA and Automation / Alerts and notifications) — every 30 seconds,
            // so a deferred (quiet-hours) or retried external notification does not sit long.
            var outboxJobKey = new JobKey(nameof(OutboxDispatcherJob));
            q.AddJob<OutboxDispatcherJob>(opts => opts.WithIdentity(outboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(outboxJobKey)
                .WithIdentity($"{nameof(OutboxDispatcherJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInSeconds(30).RepeatForever()));

            // Abandon stale chat sessions (Communication Channels / Live chat) — every 5 minutes, so
            // a visitor who closed the tab without ending the chat does not sit in the queue forever.
            var abandonChatJobKey = new JobKey(nameof(AbandonStaleChatSessionsJob));
            q.AddJob<AbandonStaleChatSessionsJob>(opts => opts.WithIdentity(abandonChatJobKey));
            q.AddTrigger(opts => opts
                .ForJob(abandonChatJobKey)
                .WithIdentity($"{nameof(AbandonStaleChatSessionsJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

            // Web form submission retry (Communication Channels / Web forms) — every 10 minutes; see
            // the job's own remarks for why this has no per-row backoff.
            var webFormRetryJobKey = new JobKey(nameof(WebFormSubmissionRetryJob));
            q.AddJob<WebFormSubmissionRetryJob>(opts => opts.WithIdentity(webFormRetryJobKey));
            q.AddTrigger(opts => opts
                .ForJob(webFormRetryJobKey)
                .WithIdentity($"{nameof(WebFormSubmissionRetryJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));
        });
        services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

        return services;
    }
}
