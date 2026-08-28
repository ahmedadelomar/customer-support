using CustomerSupport.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedReason).HasMaxLength(200);

        // Refresh lookups hit this on every token rotation.
        b.HasIndex(x => x.TokenHash).IsUnique();

        // Used when revoking a user's whole chain after reuse is detected.
        b.HasIndex(x => new { x.UserId, x.RevokedAt });

        // IsActive is derived from RevokedAt and ExpiresAt; it must not become a column.
        b.Ignore(x => x.IsActive);
    }
}

/// <summary>
/// Counter table used only by providers without sequences (SQLite in development).
/// On SQL Server the real sequences are used instead and this table stays empty.
/// </summary>
public class ReferenceCounterConfiguration : IEntityTypeConfiguration<ReferenceCounter>
{
    public void Configure(EntityTypeBuilder<ReferenceCounter> b)
    {
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}
