using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Files;
using CustomerSupport.Application.Tickets.Assignment;
using CustomerSupport.Infrastructure.Identity;
using CustomerSupport.Infrastructure.Jobs;
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
                sp.GetRequiredService<AuditLogInterceptor>());
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
        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
        services.AddScoped<ITicketEventRecorder, TicketEventRecorder>();
        services.AddScoped<IInteractionRecorder, InteractionRecorder>();
        services.AddScoped<IUserDisplayNameResolver, UserDisplayNameResolver>();
        services.AddScoped<IAgentDirectory, AgentDirectory>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IAgentCapacityService, AgentCapacityService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IContactVerificationSender, LoggingContactVerificationSender>();
        services.AddScoped<IAttachmentOwnerAuthorizer, AttachmentOwnerAuthorizer>();
        services.AddScoped<ISlaEngine, NoOpSlaEngine>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IAttachmentPolicyProvider, AttachmentPolicyProvider>();
        services.AddSingleton<IAutoCloseSettingsProvider, AutoCloseSettingsProvider>();
        services.AddSingleton<IVirusScanner, NoOpVirusScanner>();
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
        });
        services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

        return services;
    }
}
