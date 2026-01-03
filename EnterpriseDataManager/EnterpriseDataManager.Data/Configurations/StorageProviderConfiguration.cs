namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class StorageProviderConfiguration : IEntityTypeConfiguration<StorageProvider>
{
    public void Configure(EntityTypeBuilder<StorageProvider> builder)
    {
        builder.ToTable("StorageProviders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Endpoint)
            .HasMaxLength(2000);

        builder.Property(x => x.BucketOrContainer)
            .HasMaxLength(255);

        builder.Property(x => x.RootPath)
            .HasMaxLength(2000);

        builder.Property(x => x.CredentialsReference)
            .HasMaxLength(500);

        builder.Property(x => x.IsImmutable)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.QuotaBytes);

        builder.HasMany(x => x.ArchivePlans)
            .WithOne(p => p.StorageProvider)
            .HasForeignKey(p => p.StorageProviderId)
            .OnDelete(DeleteBehavior.SetNull);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.IsEnabled);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<StorageProvider> builder)
    {
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<StorageProvider> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.IsDeleted);
    }
}
