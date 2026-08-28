using CustomerSupport.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(8).IsRequired();
        builder.Property(x => x.ResolutionNote).HasMaxLength(4000);

        builder.HasIndex(x => x.Number).IsUnique().HasFilter("[IsDeleted] = 0");

        // The agent queue: my open tickets, newest first.
        builder.HasIndex(x => new { x.AssignedAgentId, x.StatusId, x.CreatedAt });

        // The team queue and the department backlog.
        builder.HasIndex(x => new { x.AssignedTeamId, x.StatusId });
        builder.HasIndex(x => new { x.BranchId, x.DepartmentId, x.StatusId });

        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });

        // Scanned by the escalation job every minute, so it must be covered.
        builder.HasIndex(x => new { x.ResolutionDueAt, x.IsResolutionBreached });
        builder.HasIndex(x => new { x.FirstResponseDueAt, x.IsFirstResponseBreached });

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lookups must never cascade: deleting a category cannot delete tickets.
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Priority)
            .WithMany()
            .HasForeignKey(x => x.PriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.Ticket)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Events)
            .WithOne(x => x.Ticket)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Tags)
            .WithOne(x => x.Ticket)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Watchers)
            .WithOne(x => x.Ticket)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.BodyText).IsRequired();
        builder.Property(x => x.AuthorDisplayName).HasMaxLength(200);
        builder.Property(x => x.ExternalMessageId).HasMaxLength(500);
        builder.Property(x => x.InReplyToExternalId).HasMaxLength(500);

        // Guarantees idempotent ingestion: a provider redelivering a webhook cannot duplicate a message.
        builder.HasIndex(x => x.ExternalMessageId)
            .IsUnique()
            .HasFilter("[ExternalMessageId] IS NOT NULL");

        builder.HasIndex(x => new { x.TicketId, x.SentAt });
    }
}

public class TicketEventConfiguration : IEntityTypeConfiguration<TicketEvent>
{
    public void Configure(EntityTypeBuilder<TicketEvent> builder)
    {
        builder.Property(x => x.Field).HasMaxLength(100);
        builder.Property(x => x.OldValue).HasMaxLength(1000);
        builder.Property(x => x.NewValue).HasMaxLength(1000);
        builder.Property(x => x.OldDisplayValue).HasMaxLength(500);
        builder.Property(x => x.NewDisplayValue).HasMaxLength(500);
        builder.Property(x => x.ActorDisplayName).HasMaxLength(200);
        builder.Property(x => x.TriggeredByRule).HasMaxLength(200);

        builder.HasIndex(x => new { x.TicketId, x.OccurredAt });
    }
}

public class TicketLookupConfiguration :
    IEntityTypeConfiguration<TicketCategory>,
    IEntityTypeConfiguration<TicketPriority>,
    IEntityTypeConfiguration<TicketStatus>,
    IEntityTypeConfiguration<Tag>,
    IEntityTypeConfiguration<TicketTag>,
    IEntityTypeConfiguration<TicketWatcher>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Path).HasMaxLength(1000).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.Path);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TicketPriority> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ColorHex).HasMaxLength(9).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(64);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Level);
    }

    public void Configure(EntityTypeBuilder<TicketStatus> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ColorHex).HasMaxLength(9).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Kind);
    }

    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ColorHex).HasMaxLength(9).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.NormalizedName }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.HasIndex(x => new { x.TicketId, x.TagId }).IsUnique();

        builder.HasOne(x => x.Tag)
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<TicketWatcher> builder)
    {
        builder.HasIndex(x => new { x.TicketId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
