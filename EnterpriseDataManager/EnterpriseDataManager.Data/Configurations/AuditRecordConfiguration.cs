namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.Property(x => x.Actor)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ResourceType)
            .HasMaxLength(256);

        builder.Property(x => x.ResourceId)
            .HasMaxLength(256);

        builder.Property(x => x.Success)
            .IsRequired();

        builder.Property(x => x.Details)
            .HasMaxLength(4000);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(1000);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(64);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.Actor);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.ResourceType);
        builder.HasIndex(x => x.Success);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
    }
}
