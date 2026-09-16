using CustomerSupport.Domain.Ai;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Files;
using CustomerSupport.Domain.Integrations;
using CustomerSupport.Domain.KnowledgeBase;
using CustomerSupport.Domain.Portal;
using CustomerSupport.Domain.Reporting;
using CustomerSupport.Domain.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

/// <summary>Channels, agent workspace, knowledge base, AI, reporting and integration tables.</summary>
public class ChannelAndContentConfiguration :
    IEntityTypeConfiguration<Channel>,
    IEntityTypeConfiguration<ChannelAccount>,
    IEntityTypeConfiguration<MessageDeliveryLog>,
    IEntityTypeConfiguration<ChatSession>,
    IEntityTypeConfiguration<ChatMessage>,
    IEntityTypeConfiguration<WebFormDefinition>,
    IEntityTypeConfiguration<WebFormSubmission>,
    IEntityTypeConfiguration<Attachment>,
    IEntityTypeConfiguration<AgentTask>,
    IEntityTypeConfiguration<Reminder>,
    IEntityTypeConfiguration<QuickReply>,
    IEntityTypeConfiguration<TicketMention>,
    IEntityTypeConfiguration<KbCategory>,
    IEntityTypeConfiguration<KbArticle>,
    IEntityTypeConfiguration<KbArticleVersion>,
    IEntityTypeConfiguration<KbArticleFeedback>,
    IEntityTypeConfiguration<KbSearchLog>,
    IEntityTypeConfiguration<AiSuggestion>,
    IEntityTypeConfiguration<AiModelConfig>,
    IEntityTypeConfiguration<ChatbotConversation>,
    IEntityTypeConfiguration<ChatbotMessage>,
    IEntityTypeConfiguration<CsatSurvey>,
    IEntityTypeConfiguration<TicketDailyMetric>,
    IEntityTypeConfiguration<ReportDefinition>,
    IEntityTypeConfiguration<ScheduledReport>,
    IEntityTypeConfiguration<Dashboard>,
    IEntityTypeConfiguration<DashboardWidget>,
    IEntityTypeConfiguration<ApiClient>,
    IEntityTypeConfiguration<IntegrationConnection>,
    IEntityTypeConfiguration<IntegrationSyncLog>,
    IEntityTypeConfiguration<Webhook>,
    IEntityTypeConfiguration<WebhookDelivery>,
    IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<Channel> b)
    {
        b.Property(x => x.Icon).HasMaxLength(64);
        b.HasIndex(x => x.Key).IsUnique();

        b.HasMany(x => x.Accounts)
            .WithOne(x => x.Channel)
            .HasForeignKey(x => x.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ChannelAccount> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Identifier).HasMaxLength(256).IsRequired();
        b.Property(x => x.LastPollError).HasMaxLength(2000);

        // Inbound routing resolves a provider address to exactly one account.
        b.HasIndex(x => new { x.ChannelId, x.Identifier }).IsUnique().HasFilter("[IsDeleted] = 0");
    }

    public void Configure(EntityTypeBuilder<MessageDeliveryLog> b)
    {
        b.Property(x => x.Recipient).HasMaxLength(256).IsRequired();
        b.Property(x => x.ProviderMessageId).HasMaxLength(256);
        b.Property(x => x.ProviderName).HasMaxLength(64);
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        // EstimatedCost's precision comes from AppDbContext.ApplyDecimalPrecision (18,4) — applied
        // to every decimal property repo-wide, so it is not set again here.

        // Provider status webhooks look the row up by their own id.
        b.HasIndex(x => x.ProviderMessageId);
        b.HasIndex(x => new { x.TicketMessageId, x.Attempt });
    }

    public void Configure(EntityTypeBuilder<ChatSession> b)
    {
        b.Property(x => x.VisitorKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.VisitorName).HasMaxLength(200);
        b.Property(x => x.VisitorEmail).HasMaxLength(256);
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.PageUrl).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();

        // The chat console polls waiting and active sessions for a team.
        b.HasIndex(x => new { x.Status, x.QueuedForTeamId, x.StartedAt });
        b.HasIndex(x => x.VisitorKey);
        b.HasIndex(x => x.CustomerId);

        b.HasMany(x => x.Messages)
            .WithOne(x => x.ChatSession)
            .HasForeignKey(x => x.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.Property(x => x.Body).IsRequired();
        b.Property(x => x.AuthorDisplayName).HasMaxLength(200);
        b.HasIndex(x => new { x.ChatSessionId, x.SentAt });
    }

    public void Configure(EntityTypeBuilder<WebFormDefinition> b)
    {
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");

        b.HasMany<WebFormSubmission>()
            .WithOne(x => x.WebFormDefinition)
            .HasForeignKey(x => x.WebFormDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<WebFormSubmission> b)
    {
        b.Property(x => x.SubmitterName).HasMaxLength(200);
        b.Property(x => x.SubmitterEmail).HasMaxLength(256);
        b.Property(x => x.SubmitterPhone).HasMaxLength(32);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.FailureReason).HasMaxLength(2000);

        // The retry job picks up submissions still awaiting processing.
        b.HasIndex(x => new { x.Status, x.SubmittedAt });

        // Rate limiting counts recent submissions per source address.
        b.HasIndex(x => new { x.IpAddress, x.SubmittedAt });
    }

    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.Property(x => x.OwnerType).HasMaxLength(64).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(400).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Checksum).HasMaxLength(128);
        b.Property(x => x.ScanResult).HasMaxLength(200);

        // Attachments are always fetched for one owner record.
        b.HasIndex(x => new { x.OwnerType, x.OwnerId });
    }

    public void Configure(EntityTypeBuilder<AgentTask> b)
    {
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);

        // The workspace shows my open tasks by due date.
        b.HasIndex(x => new { x.AssignedToId, x.Status, x.DueAt });
        b.HasIndex(x => x.TicketId);

        b.HasMany(x => x.Reminders)
            .WithOne(x => x.AgentTask)
            .HasForeignKey(x => x.AgentTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<Reminder> b)
    {
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Channels).HasMaxLength(100).IsRequired();

        // The dispatch job selects due, unsent reminders.
        b.HasIndex(x => new { x.IsSent, x.RemindAt });
        b.HasIndex(x => new { x.UserId, x.RemindAt });
    }

    public void Configure(EntityTypeBuilder<QuickReply> b)
    {
        b.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        b.Property(x => x.Shortcut).HasMaxLength(50);

        // Shortcuts must be unambiguous within one owner scope.
        b.HasIndex(x => new { x.Scope, x.OwnerId, x.TeamId, x.Shortcut })
            .HasFilter("[Shortcut] IS NOT NULL AND [IsDeleted] = 0");

        b.HasIndex(x => new { x.Scope, x.IsActive, x.UsageCount });
    }

    public void Configure(EntityTypeBuilder<TicketMention> b)
    {
        // The collaboration inbox reads unread mentions for one user.
        b.HasIndex(x => new { x.MentionedUserId, x.ReadAt, x.MentionedAt });
        b.HasIndex(x => x.TicketId);
    }

    public void Configure(EntityTypeBuilder<KbCategory> b)
    {
        b.Property(x => x.Slug).HasMaxLength(150).IsRequired();
        b.Property(x => x.Icon).HasMaxLength(64);
        b.Property(x => x.Path).HasMaxLength(1000).IsRequired();

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.Path);

        b.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Articles)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<KbArticle> b)
    {
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Keywords).HasMaxLength(1000);
        b.Property(x => x.RelatedCategoryIds).HasMaxLength(1000);

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");

        // The portal lists published public articles in a category.
        b.HasIndex(x => new { x.CategoryId, x.Status, x.IsPublic });
        b.HasIndex(x => new { x.Type, x.Status });
        b.HasIndex(x => x.ReviewDueAt);

        b.HasMany(x => x.Versions)
            .WithOne(x => x.Article)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Feedback)
            .WithOne(x => x.Article)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<KbArticleVersion> b)
    {
        b.Property(x => x.ChangeNote).HasMaxLength(1000);
        b.HasIndex(x => new { x.ArticleId, x.Version }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<KbArticleFeedback> b)
    {
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.VisitorKey).HasMaxLength(64);
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.HasIndex(x => new { x.ArticleId, x.SubmittedAt });
    }

    public void Configure(EntityTypeBuilder<KbSearchLog> b)
    {
        b.Property(x => x.Query).HasMaxLength(500).IsRequired();
        b.Property(x => x.NormalizedQuery).HasMaxLength(500).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.Source).HasMaxLength(20).IsRequired();

        // The content-gap report groups zero-result queries.
        b.HasIndex(x => new { x.ResultCount, x.NormalizedQuery });
        b.HasIndex(x => x.SearchedAt);
    }

    public void Configure(EntityTypeBuilder<AiSuggestion> b)
    {
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        b.Property(x => x.ModelId).HasMaxLength(128).IsRequired();
        b.Property(x => x.PromptVersion).HasMaxLength(32);
        b.Property(x => x.SourceHash).HasMaxLength(128);
        b.Property(x => x.RejectionReason).HasMaxLength(1000);

        // The ticket view loads the latest suggestion of each type.
        b.HasIndex(x => new { x.TicketId, x.Type, x.CreatedAt });

        // The AI usage report aggregates by status and date.
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }

    public void Configure(EntityTypeBuilder<AiModelConfig> b)
    {
        b.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        b.Property(x => x.ModelId).HasMaxLength(128).IsRequired();
        b.HasIndex(x => new { x.BranchId, x.Feature, x.IsChatbot }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<ChatbotConversation> b)
    {
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.HandoverReason).HasMaxLength(500);
        b.Property(x => x.CitedArticleIds).HasMaxLength(1000);

        // The deflection report groups by outcome over a date range.
        b.HasIndex(x => new { x.Outcome, x.StartedAt });
        b.HasIndex(x => x.ChatSessionId);

        b.HasMany(x => x.Messages)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<ChatbotMessage> b)
    {
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.RetrievedArticleIds).HasMaxLength(1000);
        b.HasIndex(x => new { x.ConversationId, x.SentAt });
    }

    public void Configure(EntityTypeBuilder<CsatSurvey> b)
    {
        b.Property(x => x.Token).HasMaxLength(64).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(2000);

        b.HasIndex(x => x.Token).IsUnique();

        // One survey per ticket, so a resolved-reopened-resolved cycle does not double-count.
        b.HasIndex(x => x.TicketId).IsUnique();

        // The satisfaction report aggregates responded surveys by agent and date.
        b.HasIndex(x => new { x.AgentId, x.RespondedAt });
    }

    public void Configure(EntityTypeBuilder<TicketDailyMetric> b)
    {
        // The natural key of the rollup. Upserted by the aggregation job.
        b.HasIndex(x => new
        {
            x.Date,
            x.BranchId,
            x.DepartmentId,
            x.TeamId,
            x.AgentId,
            x.CategoryId,
            x.PriorityId,
            x.Channel,
        }).IsUnique().HasDatabaseName("UX_TicketDailyMetric_Dimensions");

        b.HasIndex(x => x.Date);
    }

    public void Configure(EntityTypeBuilder<ReportDefinition> b)
    {
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Category).HasMaxLength(50).IsRequired();
        b.Property(x => x.DefaultVisualization).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.Key, x.OwnerId }).HasFilter("[IsDeleted] = 0");
    }

    public void Configure(EntityTypeBuilder<ScheduledReport> b)
    {
        b.Property(x => x.CronExpression).HasMaxLength(100).IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Recipients).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Format).HasMaxLength(10).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.LastRunError).HasMaxLength(2000);

        // The scheduler polls for the next due schedule.
        b.HasIndex(x => new { x.IsActive, x.NextRunAt });

        b.HasOne(x => x.ReportDefinition)
            .WithMany()
            .HasForeignKey(x => x.ReportDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<Dashboard> b)
    {
        b.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.Scope, x.OwnerId, x.RoleId });

        b.HasMany(x => x.Widgets)
            .WithOne(x => x.Dashboard)
            .HasForeignKey(x => x.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<DashboardWidget> b)
    {
        b.Property(x => x.WidgetType).HasMaxLength(20).IsRequired();
        b.Property(x => x.MetricKey).HasMaxLength(100);
        b.HasIndex(x => new { x.DashboardId, x.Row, x.Column });
    }

    public void Configure(EntityTypeBuilder<ApiClient> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.ClientId).HasMaxLength(64).IsRequired();
        b.Property(x => x.ClientSecretHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.SecretHint).HasMaxLength(8);
        b.Property(x => x.Scopes).HasMaxLength(4000).IsRequired();
        b.Property(x => x.AllowedIpRanges).HasMaxLength(2000);

        b.HasIndex(x => x.ClientId).IsUnique();
    }

    public void Configure(EntityTypeBuilder<IntegrationConnection> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        b.Property(x => x.BaseUrl).HasMaxLength(500);
        b.Property(x => x.LastHealthCheckError).HasMaxLength(2000);

        b.HasIndex(x => new { x.BranchId, x.Type, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.Type, x.IsActive });
    }

    public void Configure(EntityTypeBuilder<IntegrationSyncLog> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.Watermark).HasMaxLength(200);
        b.Property(x => x.Error).HasMaxLength(4000);
        b.Property(x => x.CorrelationId).HasMaxLength(64);

        // Incremental syncs read the last successful watermark for a connection and entity.
        b.HasIndex(x => new { x.ConnectionId, x.EntityType, x.StartedAt });

        b.HasOne(x => x.Connection)
            .WithMany()
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<Webhook> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Secret).HasMaxLength(256).IsRequired();
        b.Property(x => x.Events).HasMaxLength(2000).IsRequired();

        b.HasIndex(x => x.IsActive);
    }

    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EventId).HasMaxLength(64).IsRequired();
        b.Property(x => x.ResponseBody).HasMaxLength(2000);
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();

        // The retry worker polls pending deliveries whose backoff has elapsed.
        b.HasIndex(x => new { x.Status, x.NextRetryAt });
        b.HasIndex(x => new { x.WebhookId, x.CreatedAt });

        b.HasOne(x => x.Webhook)
            .WithMany()
            .HasForeignKey(x => x.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.Property(x => x.Type).HasMaxLength(300).IsRequired();
        b.Property(x => x.AggregateType).HasMaxLength(100);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Property(x => x.Error).HasMaxLength(4000);

        // The dispatcher polls unprocessed rows whose next attempt is due, oldest first.
        b.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.OccurredAt })
            .HasDatabaseName("IX_OutboxMessage_Dispatch");
    }
}
