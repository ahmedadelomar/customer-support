using CustomerSupport.Domain.Identity;
using CustomerSupport.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

/// <summary>Organization, security and administration tables.</summary>
public class OrganizationConfiguration :
    IEntityTypeConfiguration<Branch>,
    IEntityTypeConfiguration<Department>,
    IEntityTypeConfiguration<Team>,
    IEntityTypeConfiguration<TeamMember>,
    IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<RolePermission>,
    IEntityTypeConfiguration<AuditLog>,
    IEntityTypeConfiguration<SystemSetting>,
    IEntityTypeConfiguration<AgentSkill>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(64);
        b.Property(x => x.PhoneNumber).HasMaxLength(32);
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }

    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");

        b.HasMany(x => x.Teams)
            .WithOne(x => x.Department)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.HasMany(x => x.Members)
            .WithOne(x => x.Team)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.DepartmentId, x.IsActive });
    }

    public void Configure(EntityTypeBuilder<TeamMember> b)
    {
        b.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
        b.HasIndex(x => x.UserId);
    }

    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Category).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Key).IsUnique();
        b.HasIndex(x => x.Category);
    }

    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();

        b.HasOne(x => x.Permission)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.UserName).HasMaxLength(256);
        b.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.CorrelationId).HasMaxLength(64);

        // The audit viewer filters by date first, then narrows by entity or actor.
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => new { x.UserId, x.OccurredAt });
    }

    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Category).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);

        // One row per key per branch; the null-branch row is the global default.
        b.HasIndex(x => new { x.BranchId, x.Key }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<AgentSkill> b)
    {
        b.HasIndex(x => new { x.UserId, x.CategoryId }).IsUnique();
        b.HasIndex(x => x.CategoryId);
    }
}
