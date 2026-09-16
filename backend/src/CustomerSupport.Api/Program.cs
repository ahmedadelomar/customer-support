using System.Text;
using System.Threading.RateLimiting;
using CustomerSupport.Api.Hubs;
using CustomerSupport.Api.Infrastructure;
using CustomerSupport.Api.Services;
using CustomerSupport.Application;
using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Infrastructure;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Persistence.Interceptors;
using CustomerSupport.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging -----------------------------------------------------------------------------------
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// --- Layers ------------------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Request context ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<IAuditContextAccessor, AuditContextAccessor>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddScoped<IChatRealtimeNotifier, SignalRChatNotifier>();

// --- Authentication ----------------------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it via user-secrets or the environment.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            // No leeway: an expired token is expired. The refresh flow exists for this.
            ClockSkew = TimeSpan.Zero,
        };

        // The live-chat and notification hubs pass the token as a query parameter, because the
        // browser WebSocket API cannot set an Authorization header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// --- Rate limiting --------------------------------------------------------------------------------
// Sign-in only: 10 attempts per IP per minute. Enough for a person mistyping a password, not enough
// for credential stuffing. Queue limit 0 — a refused attempt fails fast rather than waiting in line.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Login, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Coarse per-IP ceiling across every public form combined — the precise per-form limit is the
    // database check inside SubmitWebFormCommand; this just stops one address from hammering the
    // endpoint before that query even runs.
    options.AddPolicy(RateLimitPolicies.WebFormSubmit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// --- CORS: the Angular dev server and the deployed front end ------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4300"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- MVC, errors and API docs -------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

// --- Real-time (Agent Dashboard / Team collaboration) -------------------------------------------
// First hub in this codebase — CS-303 (live chat) is specified to reuse it rather than stand up a
// second transport, so a future ChatHub maps onto this same AddSignalR() call.
builder.Services.AddSignalR();
builder.Services.AddSingleton<PresenceTracker>();

builder.Services.AddRequestLocalization(options =>
{
    // Arabic is the default culture; the Accept-Language header or the ?culture= query overrides it.
    options.SetDefaultCulture("ar")
        .AddSupportedCultures("ar", "en")
        .AddSupportedUICultures("ar", "en");
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// --- Pipeline ----------------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// After authentication so the claim is available; before the endpoints so no handler can forget it.
app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapControllers();
app.MapHub<CollaborationHub>("/hubs/collaboration");
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");

// --- Migrate and seed --------------------------------------------------------------------------
// Applying migrations at startup suits a single-instance deployment. For multi-instance, run
// `dotnet ef database update` in the release pipeline instead and set Database:AutoMigrate to false.
if (app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (db.Database.IsSqlite())
    {
        // The committed migrations are SQL Server specific (they create sequences). For the
        // development-only SQLite database, build the schema straight from the model instead —
        // it is a throwaway file, so there is no migration history worth keeping.
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

await app.RunAsync();
