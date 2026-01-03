namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.ToTable("RetentionPolicies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.RetentionPeriod)
            .IsRequired()
            .HasConversion(
                v => v.Ticks,
                v => TimeSpan.FromTicks(v));

        builder.Property(x => x.IsLegalHold)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsImmutable)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.Scope)
            .HasMaxLength(500);

        builder.HasMany(x => x.ArchivePlans)
            .WithOne(p => p.RetentionPolicy)
            .HasForeignKey(p => p.RetentionPolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.IsLegalHold);
        builder.HasIndex(x => x.IsImmutable);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.IsDeleted);
    }
}
