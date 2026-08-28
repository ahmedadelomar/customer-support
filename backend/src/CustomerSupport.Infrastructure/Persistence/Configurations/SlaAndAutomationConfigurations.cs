using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

/// <summary>SLA, automation and notification tables.</summary>
public class SlaAndAutomationConfiguration :
    IEntityTypeConfiguration<SlaPolicy>,
    IEntityTypeConfiguration<SlaTarget>,
    IEntityTypeConfiguration<TicketSlaClock>,
    IEntityTypeConfiguration<BusinessCalendar>,
    IEntityTypeConfiguration<BusinessHour>,
    IEntityTypeConfiguration<Holiday>,
    IEntityTypeConfiguration<AssignmentRule>,
    IEntityTypeConfiguration<EscalationRule>,
    IEntityTypeConfiguration<AutomationRunLog>,
    IEntityTypeConfiguration<Notification>,
    IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> b)
    {
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.BranchId, x.EvaluationOrder });

        b.HasMany(x => x.Targets)
            .WithOne(x => x.SlaPolicy)
            .HasForeignKey(x => x.SlaPolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Conditions)
            .WithOne(x => x.SlaPolicy)
            .HasForeignKey(x => x.SlaPolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.BusinessCalendar)
            .WithMany()
            .HasForeignKey(x => x.BusinessCalendarId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SlaTarget> b) =>
        b.HasIndex(x => new { x.SlaPolicyId, x.PriorityId }).IsUnique();

    public void Configure(EntityTypeBuilder<TicketSlaClock> b)
    {
        b.HasIndex(x => new { x.TicketId, x.TargetType }).IsUnique();

        // The breach sweep runs every minute and selects running clocks past their deadline.
        b.HasIndex(x => new { x.Status, x.DueAt });
    }

    public void Configure(EntityTypeBuilder<BusinessCalendar> b)
    {
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();

        b.HasMany(x => x.BusinessHours)
            .WithOne(x => x.BusinessCalendar)
            .HasForeignKey(x => x.BusinessCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Holidays)
            .WithOne(x => x.BusinessCalendar)
            .HasForeignKey(x => x.BusinessCalendarId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<BusinessHour> b) =>
        b.HasIndex(x => new { x.BusinessCalendarId, x.DayOfWeek });

    public void Configure(EntityTypeBuilder<Holiday> b) =>
        b.HasIndex(x => new { x.BusinessCalendarId, x.Date });

    public void Configure(EntityTypeBuilder<AssignmentRule> b)
    {
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.BranchId, x.IsActive, x.EvaluationOrder });
    }

    public void Configure(EntityTypeBuilder<EscalationRule> b)
    {
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.BranchId, x.IsActive, x.EvaluationOrder });
    }

    public void Configure(EntityTypeBuilder<AutomationRunLog> b)
    {
        b.Property(x => x.RuleType).HasMaxLength(40).IsRequired();
        b.Property(x => x.RuleName).HasMaxLength(200);
        b.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000);

        // Cooldown checks read the most recent run of one rule against one ticket.
        b.HasIndex(x => new { x.TicketId, x.RuleId, x.OccurredAt });
        b.HasIndex(x => x.OccurredAt);
    }

    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Link).HasMaxLength(500);
        b.Property(x => x.EntityType).HasMaxLength(64);
        b.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        b.Property(x => x.DispatchedChannels).HasMaxLength(200);

        // The bell menu reads one user's unread notifications, newest first.
        b.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
    }

    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.UserId, x.EventType }).IsUnique();
    }
}
