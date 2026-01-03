namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ArchiveJobConfiguration : IEntityTypeConfiguration<ArchiveJob>
{
    public void Configure(EntityTypeBuilder<ArchiveJob> builder)
    {
        builder.ToTable("ArchiveJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ScheduledAt);
        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        builder.Property(x => x.TotalItemCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.ProcessedItemCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.FailedItemCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.TotalBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(x => x.ProcessedBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(x => x.TargetPath)
            .HasMaxLength(2000);

        builder.HasOne(x => x.ArchivePlan)
            .WithMany(p => p.ArchiveJobs)
            .HasForeignKey(x => x.ArchivePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne(i => i.ArchiveJob)
            .HasForeignKey(i => i.ArchiveJobId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsTerminal);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ArchivePlanId);
        builder.HasIndex(x => x.ScheduledAt);
        builder.HasIndex(x => x.CreatedAt);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<ArchiveJob> builder)
    {
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<ArchiveJob> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.IsDeleted);
    }
}
