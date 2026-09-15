using CustomerSupport.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.CompanyName).HasMaxLength(250);
        builder.Property(x => x.NationalIdOrCr).HasMaxLength(50);
        builder.Property(x => x.TaxNumber).HasMaxLength(50);
        builder.Property(x => x.PrimaryEmail).HasMaxLength(256);
        builder.Property(x => x.PrimaryPhone).HasMaxLength(32);
        builder.Property(x => x.PreferredLanguage).HasMaxLength(8).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(64);
        builder.Property(x => x.Tier).HasMaxLength(50);
        builder.Property(x => x.BlockedReason).HasMaxLength(500);

        // Unique only among live rows, so a soft-deleted customer does not block reusing its code.
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.PrimaryEmail);
        builder.HasIndex(x => x.PrimaryPhone);
        builder.HasIndex(x => x.NationalIdOrCr);
        builder.HasIndex(x => new { x.BranchId, x.IsActive });
        builder.HasIndex(x => x.LastInteractionAt);

        builder.HasMany(x => x.Contacts)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Notes)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Interactions)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        builder.Property(x => x.Value).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedValue).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.CountryCode).HasMaxLength(8);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.AddressLine).HasMaxLength(500);
        builder.Property(x => x.PostalCode).HasMaxLength(20);

        // Drives inbound-message matching: an incoming email or number must resolve to one customer.
        builder.HasIndex(x => new { x.Type, x.NormalizedValue })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.CustomerId, x.Type, x.IsPrimary });
    }
}

public class ContactVerificationConfiguration : IEntityTypeConfiguration<ContactVerification>
{
    public void Configure(EntityTypeBuilder<ContactVerification> builder)
    {
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();

        builder.HasOne(x => x.CustomerContact)
            .WithMany()
            .HasForeignKey(x => x.CustomerContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // Both the "any active code?" and the rate-limit checks filter by contact and time.
        builder.HasIndex(x => new { x.CustomerContactId, x.ExpiresAt });
    }
}

public class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.CustomerId, x.IsPinned, x.CreatedAt });
    }
}

public class InteractionConfiguration : IEntityTypeConfiguration<Interaction>
{
    public void Configure(EntityTypeBuilder<Interaction> builder)
    {
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Preview).HasMaxLength(1000);
        builder.Property(x => x.SourceType).HasMaxLength(64);

        // The timeline is always read newest-first for one customer.
        builder.HasIndex(x => new { x.CustomerId, x.OccurredAt });
        builder.HasIndex(x => x.TicketId);
    }
}
