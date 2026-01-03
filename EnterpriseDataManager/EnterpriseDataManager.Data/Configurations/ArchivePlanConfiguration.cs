namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ArchivePlanConfiguration : IEntityTypeConfiguration<ArchivePlan>
{
    public void Configure(EntityTypeBuilder<ArchivePlan> builder)
    {
        builder.ToTable("ArchivePlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.OwnsOne(x => x.Schedule, schedule =>
        {
            schedule.Property(s => s.Expression)
                .HasColumnName("ScheduleExpression")
                .HasMaxLength(100);

            schedule.Property(s => s.Description)
                .HasColumnName("ScheduleDescription")
                .HasMaxLength(500);
        });

        builder.OwnsOne(x => x.SourcePath, path =>
        {
            path.Property(p => p.Path)
                .HasColumnName("SourcePath")
                .IsRequired()
                .HasMaxLength(2000);
        });

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.SecurityLevel)
            .IsRequired()
            .HasConversion<int>();

        builder.HasOne(x => x.StorageProvider)
            .WithMany(s => s.ArchivePlans)
            .HasForeignKey(x => x.StorageProviderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RetentionPolicy)
            .WithMany(r => r.ArchivePlans)
            .HasForeignKey(x => x.RetentionPolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.ArchiveJobs)
            .WithOne(j => j.ArchivePlan)
            .HasForeignKey(j => j.ArchivePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.LastRunAt);
        builder.Property(x => x.NextRunAt);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.NextRunAt);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<ArchivePlan> builder)
    {
        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<ArchivePlan> builder)
    {
        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DeletedAt);

        builder.Property(x => x.DeletedBy)
            .HasMaxLength(256);

        builder.HasIndex(x => x.IsDeleted);
    }
}
