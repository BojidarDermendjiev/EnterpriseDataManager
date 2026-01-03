namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class RecoveryJobConfiguration : IEntityTypeConfiguration<RecoveryJob>
{
    public void Configure(EntityTypeBuilder<RecoveryJob> builder)
    {
        builder.ToTable("RecoveryJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DestinationPath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);

        builder.Property(x => x.TotalItems)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.RecoveredItems)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.TotalBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(x => x.RecoveredBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.HasOne(x => x.ArchiveJob)
            .WithMany()
            .HasForeignKey(x => x.ArchiveJobId)
            .OnDelete(DeleteBehavior.Restrict);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.ArchiveJobId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<RecoveryJob> builder)
    {
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<RecoveryJob> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.IsDeleted);
    }
}
